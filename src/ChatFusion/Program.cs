using ChatFusion;

var config = AppConfig.FromEnvironment();
var mode = args.FirstOrDefault()?.ToLowerInvariant() ?? "bot";

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

switch (mode)
{
    case "unread":
        if (!config.HasAccount)
            return Fail("Set TG_API_ID and TG_API_HASH (from my.telegram.org).");
        await using (var account = new AccountClient(config))
        {
            Console.WriteLine($"Logged in as {await account.LoginAsync()}");
            Console.WriteLine(UnreadFormatter.Format(await account.GetUnreadAsync()));
        }
        return 0;

    case "bot":
        if (config.BotToken is null)
            return Fail("Set TG_BOT_TOKEN (from @BotFather).");
        AccountClient? linked = null;
        if (config.HasAccount)
        {
            linked = new AccountClient(config);
            Console.WriteLine($"Account linked: {await linked.LoginAsync()}");
        }
        await new BotHost(config, linked).RunAsync(cts.Token);
        if (linked is not null)
            await linked.DisposeAsync();
        return 0;

    default:
        return Fail("Usage: ChatFusion [bot|unread]");
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}
