using ChatFusion.Identity.Api.Infrastructure.Security;

namespace ChatFusion.Identity.Api.Infrastructure.Hosting;

/// <summary>
/// Команды, которые выполняются без запуска веб-сервера.
/// </summary>
public static class Commands
{
    public const string GenerateKey = "generate-key";
    public const string HealthCheck = "healthcheck";

    /// <summary>
    /// <c>generate-key &lt;путь&gt;</c>: создаёт закрытый ключ RSA для подписи токенов.
    /// Существующий файл не перезаписывается.
    /// </summary>
    public static int RunGenerateKey(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath))
        {
            Console.Error.WriteLine($"Файл уже существует и не будет перезаписан: {fullPath}");
            return 1;
        }

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, SigningKey.GeneratePrivateKeyPem());

        var key = SigningKey.LoadFromPemFile(fullPath, requirePrivateKey: true);
        Console.WriteLine($"Ключ подписи создан: {fullPath}");
        Console.WriteLine($"kid: {key.KeyId}");
        return 0;
    }

    /// <summary>
    /// <c>healthcheck [адрес]</c>: проверка состояния для Docker HEALTHCHECK.
    /// Нужна, потому что в chiseled-образе нет curl и оболочки.
    /// </summary>
    public static async Task<int> RunHealthCheckAsync(string? url)
    {
        var port = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")?.Split(';', ',')[0] ?? "8080";
        var target = url ?? $"http://localhost:{port}/health/live";

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            using var response = await client.GetAsync(new Uri(target));
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            Console.Error.WriteLine($"Проверка состояния не прошла: {exception.Message}");
            return 1;
        }
    }
}
