using System.Security.Claims;

namespace ChatNode.Infrastructure.Tools;

public static class ClaimsExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return value is null ? Guid.Empty : Guid.Parse(value);
    }

    public static string GetUserName(this ClaimsPrincipal user)
    {
        return user.FindFirst("name")?.Value ?? string.Empty;
    }
}