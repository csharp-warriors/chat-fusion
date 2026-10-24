# chat-fusion

Telegram helper in C# (.NET 8) with two parts:

- **Bot** (Bot API, `Telegram.Bot`) with the commands `/start`, `/help` and `/summary`.
- **Personal account** (MTProto, `WTelegramClient`) that reads the chat list and unread counts.

When both parts are configured, the bot's `/summary` command sends the owner a summary of unread messages in the personal account.

## Setup

All settings come from environment variables:

| Variable | Where to get it |
|---|---|
| `TG_BOT_TOKEN` | @BotFather |
| `TG_OWNER_ID` | your Telegram id (the bot shows it on `/start`) |
| `TG_API_ID`, `TG_API_HASH` | https://my.telegram.org → API development tools |
| `TG_PHONE` | optional; asked in the console if not set |
| `TG_SESSION_PATH` | optional, defaults to `account.session` |

> The session file grants full access to your account. Keep it on your own machine; it is listed in `.gitignore`.

## Run

```bash
dotnet run --project src/ChatFusion -- unread   # print unread chats of your account
dotnet run --project src/ChatFusion -- bot      # start the bot (links the account if TG_API_ID is set)
dotnet test
```

On the first account login, enter the code Telegram sends you (and your 2FA password, if you have one) in the console.
