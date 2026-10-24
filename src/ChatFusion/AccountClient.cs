using TL;

namespace ChatFusion;

public sealed record UnreadChat(string Title, int UnreadCount);

/// <summary>Access to the personal Telegram account via MTProto (WTelegramClient).</summary>
public sealed class AccountClient : IAsyncDisposable
{
    private readonly AppConfig _config;
    private readonly WTelegram.Client _client;

    public AccountClient(AppConfig config)
    {
        _config = config;
        WTelegram.Helpers.Log = (_, _) => { };
        _client = new WTelegram.Client(ConfigValue);
    }

    public async Task<string> LoginAsync()
    {
        var user = await _client.LoginUserIfNeeded();
        return user.ToString();
    }

    public async Task<IReadOnlyList<UnreadChat>> GetUnreadAsync()
    {
        var dialogs = await _client.Messages_GetAllDialogs();
        return dialogs.Dialogs
            .OfType<Dialog>()
            .Where(d => d.unread_count > 0)
            .Select(d => new UnreadChat(dialogs.UserOrChat(d.peer)?.ToString() ?? "(unknown)", d.unread_count))
            .OrderByDescending(c => c.UnreadCount)
            .ToList();
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    // WTelegramClient asks for its settings through this callback; codes are typed in the console.
    private string? ConfigValue(string what) => what switch
    {
        "api_id" => _config.ApiId,
        "api_hash" => _config.ApiHash,
        "session_pathname" => _config.SessionPath,
        "phone_number" => _config.Phone ?? Prompt("Phone number: "),
        "verification_code" => Prompt("Code from Telegram: "),
        "password" => Prompt("2FA password: "),
        _ => null,
    };

    private static string? Prompt(string text)
    {
        Console.Write(text);
        return Console.ReadLine();
    }
}
