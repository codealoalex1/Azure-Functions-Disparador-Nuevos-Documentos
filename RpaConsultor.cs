using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace RpaDisparadorFunction;

public class RpaConsultor
{
    private readonly ILogger<RpaDisparador> _logger;
    private readonly BlobServiceClient _blobServiceClient;

    public RpaConsultor(ILogger<RpaDisparador> logger, BlobServiceClient blobServiceClient)
    {
        _logger = logger;
        _blobServiceClient = blobServiceClient;
    }

    [Function("VerificarNuevosArchivos")]
    public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post" /*, Route = "blobs/procesar-json" */)] HttpRequest req)
    {
        _logger.LogInformation("Iniciando escaneo, filtrado y actualización de archivos JSON.");

        string containerName = "registros-json";
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        List<object> listaResultados = new List<object>();

        try
        {
            /* Iterar sobre todos los archivos obtenidos en el contenedor */
            await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
            {
                /* Filtrar únicamente .json */
                if (blobItem.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    var blobClient = containerClient.GetBlobClient(blobItem.Name);

                    // 1. Descargar el contenido actual
                    var descarga = await blobClient.DownloadContentAsync();
                    string contenidoJson = descarga.Value.Content.ToString();

                    // 2. Parsear como un JsonNode
                    var jsonNode = JsonNode.Parse(contenidoJson);

                    if (jsonNode is JsonObject jsonObject)
                    {
                        // 3. VALIDACIÓN: Si "Procesado" existe y es true, ignoramos el archivo
                        if (jsonObject.TryGetPropertyValue("Procesado", out var nodoProcesado) &&
                            nodoProcesado != null &&
                            (bool)nodoProcesado.AsValue())
                        {
                            _logger.LogInformation($"El archivo {blobItem.Name} ya fue procesado previamente");
                            continue; // Salta al siguiente archivo del bucle foreach
                        }

                        // --- Lógica para archivos NUEVOS o NO PROCESADOS ---
                        _logger.LogInformation($"Procesando archivo nuevo: {blobItem.Name}");

                        // 4. Modificar o añadir la propiedad "Procesado"
                        jsonObject["Procesado"] = true;
                        jsonObject["FechaProcesamiento"] = DateTime.UtcNow.ToString("o");

                        // 5. Convertir el objeto modificado nuevamente a un string JSON
                        string jsonActualizado = jsonObject.ToJsonString(new JsonSerializerOptions
                        {
                            WriteIndented = true
                        });

                        // 6. Sobrescribir el archivo en Azure Blob Storage
                        await blobClient.UploadAsync(new BinaryData(jsonActualizado), overwrite: true);

                        _logger.LogInformation($"Archivo {blobItem.Name} procesado y guardado exitosamente.");

                        listaResultados.Add(new
                        {
                            Ruta = blobItem.Name,
                            Datos = jsonObject
                        });
                    }
                    else
                    {
                        _logger.LogWarning($"El archivo {blobItem.Name} no tiene una estructura de objeto JSON válida.");
                    }
                }
            }

            return new OkObjectResult(new
            {
                Mensaje = "Proceso completado.",
                TotalNuevosProcesados = listaResultados.Count,
                ArchivosProcesadosEnEstaTanda = listaResultados
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error procesando o actualizando los blobs: {ex.Message}");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
}