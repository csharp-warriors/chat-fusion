namespace ChatFusion;

/// <summary>Settings read from environment variables. Secrets never live in the repository.</summary>
public sealed record AppConfig(
    string? BotToken,
    long? OwnerId,
    string? ApiId,
    string? ApiHash,
    string? Phone,
    string SessionPath)
{
    public bool HasAccount => !string.IsNullOrEmpty(ApiId) && !string.IsNullOrEmpty(ApiHash);

    public static AppConfig FromEnvironment() => new(
        BotToken: Env("TG_BOT_TOKEN"),
        OwnerId: long.TryParse(Env("TG_OWNER_ID"), out var id) ? id : null,
        ApiId: Env("TG_API_ID"),
        ApiHash: Env("TG_API_HASH"),
        Phone: Env("TG_PHONE"),
        SessionPath: Env("TG_SESSION_PATH") ?? "account.session");

    private static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
