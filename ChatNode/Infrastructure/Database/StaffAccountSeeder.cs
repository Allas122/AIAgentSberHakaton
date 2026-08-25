using ChatNode.Infrastructure.Auth.Abstractions;
using ChatNode.Infrastructure.Configuration.Options;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChatNode.Infrastructure.Database;

public class StaffAccountSeeder(
    IServiceScopeFactory scopeFactory,
    IOptions<StaffAccountsOptions> options,
    IPasswordHasher passwordHasher,
    ILogger<StaffAccountSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var accounts = options.Value.Accounts;

        if (accounts.Count == 0)
        {
            logger.LogWarning(
                "Служебные аккаунты не заданы: секция StaffAccounts пуста. " +
                "Войти под проректором или координатором будет нельзя.");
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var created = 0;
        var updated = 0;

        foreach (var configured in accounts)
        {
            if (string.IsNullOrWhiteSpace(configured.Login) || string.IsNullOrWhiteSpace(configured.Password))
            {
                logger.LogWarning("Аккаунт {Login} пропущен: не задан логин или пароль", configured.Login);
                continue;
            }

            var stored = await context.UserAccounts
                .FirstOrDefaultAsync(x => x.Id == configured.Id, cancellationToken);

            if (stored is null)
            {
                context.UserAccounts.Add(new UserAccount
                {
                    Id = configured.Id == Guid.Empty ? Guid.NewGuid() : configured.Id,
                    Login = configured.Login.Trim(),
                    PasswordHash = passwordHasher.Hash(configured.Password),
                    Name = configured.Name,
                    Email = configured.Email,
                    Role = configured.Role,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });

                created++;
                continue;
            }

            if (passwordHasher.NeedsRehash(stored.PasswordHash) &&
                passwordHasher.Verify(configured.Password, stored.PasswordHash))
            {
                stored.PasswordHash = passwordHasher.Hash(configured.Password);
                stored.UpdatedAt = DateTimeOffset.UtcNow;
                updated++;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Служебные аккаунты: создано {Created}, перехешировано {Updated}. " +
            "Существующие записи из конфигурации не перезаписываются — источник истины база.",
            created, updated);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
