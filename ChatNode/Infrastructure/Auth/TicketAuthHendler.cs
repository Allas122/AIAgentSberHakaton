using System.Security.Claims;
using System.Text.Encodings.Web;
using ChatNode.Infrastructure.Auth.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ChatNode.Infrastructure.Auth;

public class TicketAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ITicketProvider ticketProvider) 
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Query.TryGetValue("ticket", out var ticketValues) || 
            !Guid.TryParse(ticketValues.FirstOrDefault(), out var ticketId))
        {
            return AuthenticateResult.NoResult();
        }
        var userId = await ticketProvider.GetUserIdByTicketAsync(ticketId);

        if (userId == Guid.Empty)
        {
            return AuthenticateResult.Fail("Ticket is invalid or expired.");
        }

        var claims = new[] { 
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()) 
        };
        
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var authTicket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(authTicket);
    }
}