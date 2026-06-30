using System.Text.Json;
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
        _logger.LogInformation("Iniciando escaneo de archivos JSON en todas las carpetas.");

        string containerName = "registros-json"; // Tu contenedor
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        List<object> listaResultados = new List<object>();

        try
        {
            // GetBlobsAsync recorre de forma plana/recursiva todas las "carpetas" por defecto
            await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
            {
                // 1. Filtrar para asegurarnos de que sea un archivo .json
                if (blobItem.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation($"Archivo JSON encontrado en la ruta: {blobItem.Name}");

                    // Obtener la referencia al cliente del blob específico
                    var blobClient = containerClient.GetBlobClient(blobItem.Name);

                    // 2. Leer o descargar el contenido del archivo JSON
                    var descarga = await blobClient.DownloadContentAsync();
                    string contenidoJson = descarga.Value.Content.ToString();

                    // 3. Aquí ya tienes el JSON. Puedes guardarlo en una lista, 
                    // deserializarlo en un objeto de C#, o procesarlo directamente.
                    using (JsonDocument doc = JsonDocument.Parse(contenidoJson))
                    {
                        JsonElement root = doc.RootElement.Clone();

                        listaResultados.Add(new
                        {
                            Datos = root
                        });
                    }
                }
            }

            return new OkObjectResult(new
            {
                TotalProcesados = listaResultados.Count,
                Archivos = listaResultados
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error procesando los blobs: {ex.Message}");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
}