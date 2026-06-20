using System.Collections.Concurrent;

namespace Archiving.Api.Common;

public sealed record OnlineUserDto(long Id, string FullName, string? Role, DateTime LastSeenAt);

public interface IOnlineUserTracker
{
    void Touch(long userId, string fullName, string? role);
    IReadOnlyList<OnlineUserDto> GetOnline();
}

public sealed class OnlineUserTracker : IOnlineUserTracker
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    private sealed record Entry(string FullName, string? Role, DateTime LastSeen);
    private readonly ConcurrentDictionary<long, Entry> _seen = new();

    public void Touch(long userId, string fullName, string? role)
        => _seen[userId] = new Entry(fullName, role, DateTime.UtcNow);

    public IReadOnlyList<OnlineUserDto> GetOnline()
    {
        var cutoff = DateTime.UtcNow - Window;
        return _seen
            .Where(kv => kv.Value.LastSeen >= cutoff)
            .Select(kv => new OnlineUserDto(kv.Key, kv.Value.FullName, kv.Value.Role, kv.Value.LastSeen))
            .OrderByDescending(u => u.LastSeenAt)
            .ToList();
    }
}
