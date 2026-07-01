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
            /* Iterar sobre todos los archivos obtenidos en el contenedor */
            await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
            {
                /* Filtrar unicamente .json */
                if (blobItem.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    /* Filtrar unicamente el archivo nuevo.json, si no existe, no se muestra nada sobre este sitio */
                    if (blobItem.Name.Split("/")[1] != "nuevo.json") { continue; }
                    _logger.LogInformation($"Archivo JSON encontrado en la ruta: {blobItem.Name}");

                    var blobClient = containerClient.GetBlobClient(blobItem.Name);

                    var descarga = await blobClient.DownloadContentAsync();
                    string contenidoJson = descarga.Value.Content.ToString();

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