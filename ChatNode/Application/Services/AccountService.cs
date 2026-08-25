using ChatNode.Application.DTO;
using ChatNode.Application.Exceptions;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.Auth.Abstractions;
using Domain.Entities;
using Domain.Repositories;
using Domain.ValueTypes;

namespace ChatNode.Application.Services;

public class AccountService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher) : IAccountService
{
    public const int MinPasswordLength = 8;
    public const int MinLoginLength = 3;

    private static readonly UserRole[] AssignableRoles =
        [UserRole.Rector, UserRole.Coordinator, UserRole.User];

    private static readonly UserRole[] StaffRoles = [UserRole.Rector, UserRole.Coordinator];

    public async Task<IReadOnlyList<AccountDto>> ListAsync(Guid actorId, UserRole actorRole)
    {
        DenyNotRector(actorRole);

        var accounts = await userRepository.ListAccountsAsync();

        return accounts.Select(account => Map(account, actorId)).ToList();
    }

    public async Task<IReadOnlyList<AccountDto>> ListCuratorsAsync(Guid actorId, UserRole actorRole)
    {
        if (!StaffRoles.Contains(actorRole))
        {
            throw new PermissionDenied("Список кураторов доступен только проректору и координатору.");
        }

        var accounts = await userRepository.ListAccountsAsync();

        return accounts
            .Where(account => StaffRoles.Contains(account.Role))
            .OrderBy(account => account.Role == UserRole.Coordinator ? 0 : 1)
            .ThenBy(account => account.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(account => Map(account, actorId))
            .ToList();
    }

    public async Task<AccountDto> CreateAsync(Guid actorId, UserRole actorRole, CreateAccountDto dto)
    {
        DenyNotRector(actorRole);
        ValidateRole(dto.Role);

        var account = await NewAccountAsync(dto.Login, dto.Password, dto.Name, dto.Email, dto.Role);

        return Map(account, actorId);
    }

    public async Task<AccountDto> RegisterAsync(RegisterAccountDto dto)
    {
        var account = await NewAccountAsync(dto.Login, dto.Password, dto.Name, dto.Email, UserRole.User);

        return Map(account, account.Id);
    }

    private async Task<UserAccount> NewAccountAsync(
        string rawLogin,
        string password,
        string name,
        string? email,
        UserRole role)
    {
        var login = Normalize(rawLogin);

        ValidateLogin(login);
        ValidatePassword(password);

        if (await userRepository.LoginTakenAsync(login))
        {
            throw new InvalidRequestException($"Логин «{login}» уже занят.");
        }

        var account = new UserAccount
        {
            Id = Guid.NewGuid(),
            Login = login,
            PasswordHash = passwordHasher.Hash(password),
            Name = Trim(name, "Без имени"),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await userRepository.CreateAccountAsync(account);

        return account;
    }

    public async Task<AccountDto> UpdateAsync(
        Guid actorId,
        UserRole actorRole,
        Guid accountId,
        UpdateAccountDto dto)
    {
        DenyNotRector(actorRole);

        var account = await userRepository.FindAccountByIdAsync(accountId)
                      ?? throw new NotFoundException("Учётная запись не найдена.");

        if (dto.Role is { } role)
        {
            ValidateRole(role);

            if (accountId == actorId && role != UserRole.Rector)
            {
                throw new InvalidRequestException(
                    "Нельзя снять с себя роль проректора — иначе некому будет управлять учётками.");
            }

            account.Role = role;
        }

        if (dto.Name is not null) account.Name = Trim(dto.Name, account.Name);
        if (dto.Email is not null) account.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();

        if (dto.Password is not null)
        {
            ValidatePassword(dto.Password);
            account.PasswordHash = passwordHasher.Hash(dto.Password);
        }

        if (!await userRepository.UpdateAccountAsync(account))
        {
            throw new NotFoundException("Учётная запись не найдена.");
        }

        return Map(account, actorId);
    }

    public async Task DeleteAsync(Guid actorId, UserRole actorRole, Guid accountId)
    {
        DenyNotRector(actorRole);

        if (accountId == actorId)
        {
            throw new InvalidRequestException("Нельзя удалить учётную запись, под которой вы работаете.");
        }

        var account = await userRepository.FindAccountByIdAsync(accountId)
                      ?? throw new NotFoundException("Учётная запись не найдена.");

        if (account.Role == UserRole.Rector)
        {
            var rectors = (await userRepository.ListAccountsAsync()).Count(x => x.Role == UserRole.Rector);

            if (rectors <= 1)
            {
                throw new InvalidRequestException("Это последний проректор — удалять его нельзя.");
            }
        }

        if (!await userRepository.DeleteAccountAsync(accountId))
        {
            throw new NotFoundException("Учётная запись не найдена.");
        }
    }

    private static void DenyNotRector(UserRole role)
    {
        if (role != UserRole.Rector)
        {
            throw new PermissionDenied("Управлять учётными записями может только проректор.");
        }
    }

    private static void ValidateRole(UserRole role)
    {
        if (!AssignableRoles.Contains(role))
        {
            throw new InvalidRequestException(
                "Роль можно назначить только из списка: Rector, Coordinator, User.");
        }
    }

    private static void ValidateLogin(string login)
    {
        if (login.Length < MinLoginLength)
        {
            throw new InvalidRequestException($"Логин короче {MinLoginLength} символов.");
        }

        if (login.Any(char.IsWhiteSpace))
        {
            throw new InvalidRequestException("В логине не должно быть пробелов.");
        }
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinPasswordLength)
        {
            throw new InvalidRequestException($"Пароль короче {MinPasswordLength} символов.");
        }
    }

    private static string Normalize(string login) => login.Trim().ToLowerInvariant();

    private static string Trim(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static AccountDto Map(UserAccount account, Guid actorId) =>
        new(account.Id,
            account.Login,
            account.Name,
            account.Email,
            account.Role,
            account.Id == actorId,
            account.CreatedAt);
}
