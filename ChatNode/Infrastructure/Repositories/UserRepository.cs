using ChatNode.Infrastructure.Configuration.Options;
using ChatNode.Infrastructure.Database;
using ChatNode.Infrastructure.Mappers;
using ChatNode.Infrastructure.Tools.Abstractions;
using Domain.Entities;
using Domain.Repositories;
using Domain.ValueTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ChatNode.Infrastructure;

public class UserRepository(
    IDatabase database,
    AppDbContext context,
    IOptions<ExpirationPolicyOption> exOptions,
    IExpirationSustainerTool expirationSustainerTool) : IUserRepository
{
    private readonly ExpirationPolicyOption _exOptions = exOptions.Value;

    public async Task<Guid> CreateGuessAsync()
    {
        var user = new User(Guid.NewGuid(), UserRole.Guest, "Гость", null, null);
        var userKey = $"user:{user.Id}";

        await database.HashSetAsync(userKey, user.ToHashEntries());
        await database.KeyExpireAsync(userKey, TimeSpan.FromSeconds(_exOptions.UserExpirationSeconds));

        return user.Id;
    }

    public async Task UpsertAsync(User user)
    {
        if (user.Role == UserRole.Guest)
        {
            var userKey = $"user:{user.Id}";

            await database.HashSetAsync(userKey, user.ToHashEntries());
            await database.KeyExpireAsync(userKey, TimeSpan.FromSeconds(_exOptions.UserExpirationSeconds));

            return;
        }

        var account = await context.UserAccounts.FirstOrDefaultAsync(x => x.Id == user.Id);
        if (account is null) return;

        account.Name = user.Name;
        account.Email = user.Email;
        account.Role = user.Role;
        account.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync();
    }

    public async Task<User?> GetUserAsync(Guid id)
    {
        var account = await context.UserAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (account is not null) return account.MapToUser();

        var userKey = $"user:{id}";
        var stored = await database.HashGetAllAsync(userKey);
        if (stored.Length == 0) return null;

        await expirationSustainerTool.SustainByUserIdAsync(id);

        return stored.ToUser(id);
    }

    public async Task<UserAccount?> FindAccountByIdAsync(Guid id) =>
        await context.UserAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);

    public async Task<IReadOnlyList<UserAccount>> ListAccountsAsync() =>
        await context.UserAccounts
            .AsNoTracking()
            .OrderBy(x => x.Role)
            .ThenBy(x => x.Login)
            .ToListAsync();

    public async Task<bool> LoginTakenAsync(string login, Guid? exceptId = null) =>
        await context.UserAccounts
            .AnyAsync(x => x.Login.ToLower() == login.ToLower() && (exceptId == null || x.Id != exceptId));

    public async Task CreateAccountAsync(UserAccount account)
    {
        context.UserAccounts.Add(account);
        await context.SaveChangesAsync();
    }

    public async Task<bool> UpdateAccountAsync(UserAccount account)
    {
        var stored = await context.UserAccounts.FirstOrDefaultAsync(x => x.Id == account.Id);
        if (stored is null) return false;

        stored.Login = account.Login;
        stored.Name = account.Name;
        stored.Email = account.Email;
        stored.Role = account.Role;
        stored.PasswordHash = account.PasswordHash;
        stored.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAccountAsync(Guid id) =>
        await context.UserAccounts.Where(x => x.Id == id).ExecuteDeleteAsync() > 0;

    public async Task<UserAccount?> FindAccountByLoginAsync(string login) =>
        await context.UserAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Login.ToLower() == login.ToLower());
}
