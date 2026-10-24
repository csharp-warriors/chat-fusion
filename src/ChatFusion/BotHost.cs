using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ChatFusion;

/// <summary>Bot API side: answers commands; /summary is available only to the owner.</summary>
public sealed class BotHost(AppConfig config, AccountClient? account)
{
    private const string Help =
        "/start — приветствие\n" +
        "/help — список команд\n" +
        "/summary — сводка непрочитанного в личном аккаунте (только владельцу)";

    public async Task RunAsync(CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var bot = new TelegramBotClient(config.BotToken!, cancellationToken: cts.Token);
        var me = await bot.GetMe();
        Console.WriteLine($"Bot @{me.Username} started. Ctrl+C to stop.");

        bot.OnError += (ex, _) =>
        {
            Console.Error.WriteLine($"Bot error: {ex.Message}");
            return Task.CompletedTask;
        };
        bot.OnMessage += (msg, type) => HandleAsync(bot, msg, type);

        try { await SendSummariesAsync(bot, cts.Token); }
        catch (OperationCanceledException) { }
    }

    // Sends the owner an unread summary every TG_SUMMARY_EVERY_HOURS hours (0 turns it off).
    private async Task SendSummariesAsync(TelegramBotClient bot, CancellationToken ct)
    {
        if (config.SummaryEveryHours <= 0 || config.OwnerId is not { } ownerId || account is null)
        {
            await Task.Delay(Timeout.Infinite, ct);
            return;
        }

        Console.WriteLine($"Auto summary every {config.SummaryEveryHours} h.");
        using var timer = new PeriodicTimer(TimeSpan.FromHours(config.SummaryEveryHours));
        do
        {
            try
            {
                var chats = await account.GetUnreadAsync();
                if (chats.Count > 0)
                    await bot.SendMessage(ownerId, UnreadFormatter.Format(chats), cancellationToken: ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.Error.WriteLine($"Auto summary failed: {ex.Message}");
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }

    private async Task HandleAsync(TelegramBotClient bot, Message msg, UpdateType type)
    {
        if (msg.Text is not { } text || !text.StartsWith('/'))
            return;

        var command = text.Split(' ', '@')[0].ToLowerInvariant();
        var reply = command switch
        {
            "/start" => $"Привет! Я бот chat-fusion. Твой Telegram id: {msg.From?.Id}\n\n{Help}",
            "/help" => Help,
            "/summary" => await SummaryAsync(msg.From?.Id),
            _ => "Не знаю такую команду. /help",
        };
        await bot.SendMessage(msg.Chat, reply);
    }

    private async Task<string> SummaryAsync(long? userId)
    {
        if (config.OwnerId is null || userId != config.OwnerId)
            return "Команда доступна только владельцу (TG_OWNER_ID).";
        if (account is null)
            return "Личный аккаунт не подключён: задайте TG_API_ID и TG_API_HASH.";

        try { return UnreadFormatter.Format(await account.GetUnreadAsync()); }
        catch (Exception ex) { return $"Не удалось получить чаты: {ex.Message}"; }
    }
}
