using ChatNode.Infrastructure.Configuration.Options;
using ChatNode.Infrastructure.Mappers;
using ChatNode.Infrastructure.Tools.Abstractions;
using Domain.Entities;
using Domain.Repositories;
using Domain.ValueTypes;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ChatNode.Infrastructure;

public class UserRepository(
    IDatabase database,
    IOptions<ExpirationPolicyOption> exOptions,
    IExpirationSustainerTool expirationSustainerTool) : IUserRepository
{
    private readonly ExpirationPolicyOption _exOptions = exOptions.Value;

    public async Task<Guid> CreateGuessAsync()
    {
        var user = new User(Guid.NewGuid(), UserRole.Guest, "Guess", null, null);
        var userKey = $"user:{user.Id}";

        await database.HashSetAsync(userKey, user.ToHashEntries());
        await database.KeyExpireAsync(userKey, TimeSpan.FromSeconds(_exOptions.UserExpirationSeconds));

        return user.Id;
    }

    public async Task<User?> GetUserAsync(Guid id)
    {
        var userKey = $"user:{id}";
        var user = await database.HashGetAllAsync(userKey);
        if (!user.Any()) return null;
        await expirationSustainerTool.SustainByUserIdAsync(id);
        return user.ToUser(id);
    }
}