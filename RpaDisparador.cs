using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace RpaDisparadorFunction;

public class RpaDisparador
{
    private readonly ILogger<RpaDisparador> _logger;

    public RpaDisparador(ILogger<RpaDisparador> logger)
    {
        _logger = logger;
    }

    [Function("RpaDisparador")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        return new OkObjectResult(JsonSerializer.Serialize(new {
            mensaje="hola mundito"
        }));
    }
}