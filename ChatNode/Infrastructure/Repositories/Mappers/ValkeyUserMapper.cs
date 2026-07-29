using Domain.Entities;
using Domain.ValueTypes;
using StackExchange.Redis;

namespace ChatNode.Infrastructure.Mappers;

public static class ValkeyUserMapper
{
    public static HashEntry[] ToHashEntries(this User user)
    {
        return new HashEntry[]
        {
            new("role", (int)user.Role),
            new("name", user.Name),
            new("email", user.Email ?? string.Empty),
            new("password", user.HashedPassword ?? string.Empty)
        };
    }

    public static User ToUser(this HashEntry[] entries, Guid id)
    {
        var dict = entries.ToDictionary(e => e.Name.ToString(), e => e.Value);

        return new User(
            id,
            dict.TryGetValue("role", out var r) ? (UserRole)(int)r : UserRole.Guest,
            dict.TryGetValue("name", out var n) ? n.ToString() : "Unknown",
            dict.TryGetValue("email", out var e) && !e.IsNullOrEmpty ? e.ToString() : null,
            dict.TryGetValue("password", out var p) && !p.IsNullOrEmpty ? p.ToString() : null
        );
    }
}