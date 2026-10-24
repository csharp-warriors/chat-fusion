using ChatFusion;

namespace ChatFusion.Tests;

public class UnreadFormatterTests
{
    [Fact]
    public void Empty_SaysNothingUnread() =>
        Assert.Equal("Непрочитанных нет.", UnreadFormatter.Format([]));

    [Fact]
    public void ListsChatsAndTotal()
    {
        var text = UnreadFormatter.Format([new("Work", 5), new("Mom", 2)]);
        Assert.StartsWith("Непрочитанные: 7 в 2 чатах", text);
        Assert.Contains("• Work — 5", text);
    }

    [Fact]
    public void TruncatesOverLimit()
    {
        var chats = Enumerable.Range(1, 5).Select(i => new UnreadChat($"c{i}", 1)).ToList();
        var text = UnreadFormatter.Format(chats, limit: 2);
        Assert.EndsWith("…и ещё 3", text);
        Assert.DoesNotContain("c3", text);
    }
}
