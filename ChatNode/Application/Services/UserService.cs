using ChatNode.Application.DTO;
using ChatNode.Application.Mappers;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.Auth.Abstractions;
using ChatNode.Infrastructure.Mappers;
using Domain.Repositories;

namespace ChatNode.Application.Services;

public class UserService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher) : IUserService
{
    public async Task<Guid> CreateGuestAsync()
    {
        return await userRepository.CreateGuessAsync();
    }

    public async Task<UserDto?> GetUserAsync(Guid id)
    {
        var user = await userRepository.GetUserAsync(id);
        return user?.MapToUserDto();
    }

    public async Task<UserDto?> LoginAsync(string login, string password)
    {
        var account = await userRepository.FindAccountByLoginAsync(login);

        if (account is null) return null;
        if (!passwordHasher.Verify(password, account.PasswordHash)) return null;

        return account.MapToUser().MapToUserDto();
    }
}
