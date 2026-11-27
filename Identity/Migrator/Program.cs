using System.Text;
using ChatFusion.Identity.Migrator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Мигратор базы данных Identity.
//   migrate (по умолчанию)  создаёт базу данных, если её нет, применяет миграции, создаёт первого администратора
//   status                  показывает применённые и ожидающие миграции, базу данных не меняет
//   script [файл]           пишет идемпотентный SQL-скрипт всех миграций, подключение к базе данных не нужно
// Окружение задаётся DOTNET_ENVIRONMENT или параметром --environment (Development, Production).

Console.OutputEncoding = Encoding.UTF8;

const string MigrateCommand = "migrate";
const string StatusCommand = "status";
const string ScriptCommand = "script";
const string DefaultScriptPath = "identity-migrations.sql";

// Первый аргумент без "--" — команда, остальные передаются конфигурации (например, --environment Production)
var command = args is [var first, ..] && !first.StartsWith('-') ? first : MigrateCommand;
var hostArgs = args is [var head, ..] && head == command ? args[1..] : args;

string? scriptPath = null;
if (command == ScriptCommand)
{
    scriptPath = hostArgs is [var path, ..] && !path.StartsWith('-') ? path : DefaultScriptPath;
    hostArgs = hostArgs is [var p, ..] && p == scriptPath ? hostArgs[1..] : hostArgs;
}

if (command is not (MigrateCommand or StatusCommand or ScriptCommand))
{
    Console.Error.WriteLine($"Неизвестная команда: {command}. Доступны: {MigrateCommand}, {StatusCommand}, {ScriptCommand} [файл].");
    return 2;
}

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = hostArgs,
    // appsettings.json ищутся рядом со сборкой, а не в текущей папке
    ContentRootPath = AppContext.BaseDirectory,
});
builder.Services.AddIdentityMigrator(builder.Configuration, requireConnectionString: command != ScriptCommand);

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ChatFusion.Identity.Migrator");

try
{
    await using var scope = host.Services.CreateAsyncScope();
    var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();

    logger.LogInformation("Команда {Command}, окружение {Environment}.", command, builder.Environment.EnvironmentName);

    switch (command)
    {
        case MigrateCommand:
            await migrator.MigrateAsync(CancellationToken.None);
            break;

        case StatusCommand:
            var status = await migrator.GetStatusAsync(CancellationToken.None);
            logger.LogInformation(
                "База данных {State}. Применено миграций: {AppliedCount}{Applied}. Ожидают применения: {PendingCount}{Pending}.",
                status.DatabaseExists ? "существует" : "не создана",
                status.Applied.Count,
                FormatList(status.Applied),
                status.Pending.Count,
                FormatList(status.Pending));
            break;

        case ScriptCommand:
            var fullPath = Path.GetFullPath(scriptPath!);
            await File.WriteAllTextAsync(fullPath, migrator.GenerateScript());
            logger.LogInformation("SQL-скрипт миграций записан в {Path}.", fullPath);
            break;
    }

    return 0;
}
catch (Exception exception)
{
    logger.LogError(exception, "Команда {Command} завершилась с ошибкой: {Message}", command, exception.Message);
    return 1;
}

static string FormatList(IReadOnlyList<string> items) => items.Count == 0 ? "" : $" ({string.Join(", ", items)})";
