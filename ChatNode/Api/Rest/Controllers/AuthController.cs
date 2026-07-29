using ChatNode.Api.Rest.Messages.Auth;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.Auth.Abstractions;
using ChatNode.Infrastructure.Tools;
using Domain.Entities;
using Domain.ValueTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace ChatNode.Api.Rest.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    IUserService userService,
    IJwtProvider jwtProvider,
    ITicketProvider ticketProvider
) : ControllerBase
{
    [HttpPost("guest")]
    public async Task<IActionResult> CreateGuest()
    {
        var guestId = await userService.CreateGuestAsync();
        return Ok(await jwtProvider.GenerateTokenPairAsync(guestId, UserRole.Guest, "guest"));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        try
        {
            var newTokens = await jwtProvider.RefreshTokensAsync(request.AccessToken, request.RefreshToken);
            return Ok(newTokens);
        }
        catch (SecurityTokenException ex)
        {
            return Unauthorized(ex.Message);
        }
    }
    
    [Authorize]
    [HttpGet("ws-ticket")]
    public async Task<IActionResult> GetTicket()
    {
        var userId = User.GetUserId(); 
        var ticket = await ticketProvider.GetTicketAsync(userId);
        return Ok(new TicketResponse(ticket));
    }
}