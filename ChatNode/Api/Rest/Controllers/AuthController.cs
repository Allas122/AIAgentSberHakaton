using ChatNode.Api.Rest.Messages.Accounts;
using ChatNode.Api.Rest.Messages.Auth;
using ChatNode.Application.DTO;
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
    IAccountService accountService,
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

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var account = await accountService.RegisterAsync(
            new RegisterAccountDto(request.Login, request.Password, request.Name, request.Email));

        return Ok(await jwtProvider.GenerateTokenPairAsync(account.Id, account.Role, account.Name));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await userService.LoginAsync(request.Login, request.Password);

        if (user is null) return Unauthorized("Неверный логин или пароль.");

        return Ok(await jwtProvider.GenerateTokenPairAsync(user.Id, user.Role, user.Name));
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