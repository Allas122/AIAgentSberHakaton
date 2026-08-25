using ChatNode.Api.Configuration;
using ChatNode.Api.Rest.Messages.Accounts;
using ChatNode.Application.DTO;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.Tools;
using Domain.ValueTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatNode.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.Staff)]
[Route("api/accounts")]
public class AccountController(IAccountService accountService) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Rector))]
    public async Task<IReadOnlyList<AccountResponse>> List()
    {
        var accounts = await accountService.ListAsync(
            HttpContext.User.GetUserId(),
            HttpContext.User.GetUserRole());

        return accounts.Select(Map).ToList();
    }

    [HttpGet("curators")]
    public async Task<IReadOnlyList<AccountResponse>> Curators()
    {
        var accounts = await accountService.ListCuratorsAsync(
            HttpContext.User.GetUserId(),
            HttpContext.User.GetUserRole());

        return accounts.Select(Map).ToList();
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Rector))]
    public async Task<AccountResponse> Create([FromBody] CreateAccountRequest request)
    {
        var account = await accountService.CreateAsync(
            HttpContext.User.GetUserId(),
            HttpContext.User.GetUserRole(),
            new CreateAccountDto(request.Login, request.Password, request.Name, request.Email, request.Role));

        return Map(account);
    }

    [HttpPatch("{accountId:guid}")]
    [Authorize(Roles = nameof(UserRole.Rector))]
    public async Task<AccountResponse> Update(Guid accountId, [FromBody] UpdateAccountRequest request)
    {
        var account = await accountService.UpdateAsync(
            HttpContext.User.GetUserId(),
            HttpContext.User.GetUserRole(),
            accountId,
            new UpdateAccountDto(request.Name, request.Email, request.Role, request.Password));

        return Map(account);
    }

    [HttpDelete("{accountId:guid}")]
    [Authorize(Roles = nameof(UserRole.Rector))]
    public async Task<IActionResult> Delete(Guid accountId)
    {
        await accountService.DeleteAsync(
            HttpContext.User.GetUserId(),
            HttpContext.User.GetUserRole(),
            accountId);

        return NoContent();
    }

    private static AccountResponse Map(AccountDto dto) =>
        new(dto.Id, dto.Login, dto.Name, dto.Email, dto.Role.ToString(), dto.IsSelf, dto.CreatedAt);
}
