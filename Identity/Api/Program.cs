using System.Text;
using ChatFusion.Identity.Api.Infrastructure.Hosting;

Console.OutputEncoding = Encoding.UTF8;

switch (args)
{
    case [Commands.GenerateKey, var keyPath]:
        return Commands.RunGenerateKey(keyPath);

    case [Commands.HealthCheck, .. var healthCheckArgs]:
        return await Commands.RunHealthCheckAsync(healthCheckArgs.FirstOrDefault());
}

// База данных и миграции создаются отдельным проектом ChatFusion.Identity.Migrator
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdentityServiceApi(builder.Configuration);

var app = builder.Build();
app.UseIdentityServiceApi();
await app.RunAsync();
return 0;

/// <summary>
/// Точка входа. Объявлена явно, чтобы интеграционные тесты могли использовать <c>WebApplicationFactory</c>.
/// </summary>
public partial class Program;
