using ChatNode.Application.DTO;
using ChatNode.Application.Mappers;
using ChatNode.Application.Services.Abstractons;
using Domain.Repositories;

namespace ChatNode.Application.Services;

public class UserService(IUserRepository userRepository) : IUserService
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
}