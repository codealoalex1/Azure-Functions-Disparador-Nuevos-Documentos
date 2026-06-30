using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Azure.Storage.Blobs;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication() // Requerido en .NET Isolated para HTTP
    .ConfigureServices(services =>
    {
        // Inyectamos el BlobServiceClient usando la cadena de conexión de tus configuraciones
        services.AddSingleton(x => {
            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage") 
                ?? "UseDevelopmentStorage=true";
            return new BlobServiceClient(connectionString);
        });
    })
    .Build();

host.Run();