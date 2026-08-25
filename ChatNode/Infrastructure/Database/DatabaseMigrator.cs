using Microsoft.EntityFrameworkCore;

namespace ChatNode.Infrastructure.Database;

public class DatabaseMigrator(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseMigrator> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

        if (pending.Count == 0)
        {
            logger.LogInformation("Схема базы данных актуальна, миграции не требуются.");
            return;
        }

        logger.LogInformation("Накатываю миграции: {Migrations}", string.Join(", ", pending));
        await context.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Миграции применены, накачено: {Count}", pending.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
