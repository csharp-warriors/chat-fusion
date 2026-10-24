using System.Text;

namespace ChatFusion;

public static class UnreadFormatter
{
    public static string Format(IReadOnlyList<UnreadChat> chats, int limit = 20)
    {
        if (chats.Count == 0)
            return "Непрочитанных нет.";

        var sb = new StringBuilder();
        sb.AppendLine($"Непрочитанные: {chats.Sum(c => c.UnreadCount)} в {chats.Count} чатах");
        foreach (var chat in chats.Take(limit))
            sb.AppendLine($"• {chat.Title} — {chat.UnreadCount}");
        if (chats.Count > limit)
            sb.AppendLine($"…и ещё {chats.Count - limit}");
        return sb.ToString().TrimEnd();
    }
}
