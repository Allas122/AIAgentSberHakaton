using ChatNode.Application.DTO;

namespace ChatNode.Application.Services.Abstractons;

public interface IUserService
{
    public Task<Guid> CreateGuestAsync();
    public Task<UserDto?> GetUserAsync(Guid id);
}