using Domain.Entities;

namespace Domain.Repositories;

public interface IUserRepository
{
    public Task<Guid> CreateGuessAsync();
    public Task<User?> GetUserAsync(Guid id);
}