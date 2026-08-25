using Domain.Entities;

namespace Domain.Repositories;

public interface IUserRepository
{
    public Task<Guid> CreateGuessAsync();
    public Task<User?> GetUserAsync(Guid id);
    public Task UpsertAsync(User user);
    public Task<UserAccount?> FindAccountByLoginAsync(string login);

    public Task<UserAccount?> FindAccountByIdAsync(Guid id);

    public Task<IReadOnlyList<UserAccount>> ListAccountsAsync();

    public Task<bool> LoginTakenAsync(string login, Guid? exceptId = null);

    public Task CreateAccountAsync(UserAccount account);

    public Task<bool> UpdateAccountAsync(UserAccount account);

    public Task<bool> DeleteAccountAsync(Guid id);
}