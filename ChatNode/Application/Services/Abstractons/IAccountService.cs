using ChatNode.Application.DTO;
using Domain.ValueTypes;

namespace ChatNode.Application.Services.Abstractons;

public interface IAccountService
{
    Task<IReadOnlyList<AccountDto>> ListAsync(Guid actorId, UserRole actorRole);

    Task<IReadOnlyList<AccountDto>> ListCuratorsAsync(Guid actorId, UserRole actorRole);

    Task<AccountDto> CreateAsync(Guid actorId, UserRole actorRole, CreateAccountDto dto);

    Task<AccountDto> RegisterAsync(RegisterAccountDto dto);

    Task<AccountDto> UpdateAsync(Guid actorId, UserRole actorRole, Guid accountId, UpdateAccountDto dto);

    Task DeleteAsync(Guid actorId, UserRole actorRole, Guid accountId);
}
