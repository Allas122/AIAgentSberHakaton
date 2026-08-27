using ChatNode.Infrastructure.AI.Services;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace ChatNode.Infrastructure.Database;

public class EmbeddingBackfill(
    IServiceScopeFactory scopeFactory,
    ILogger<EmbeddingBackfill> logger) : IHostedService
{
    private const int BatchSize = 200;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var filled = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var parts = await context.ManualParts
                .Where(part => part.EmbeddingVector == null)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (parts.Count == 0) break;

            foreach (var part in parts)
            {
                part.EmbeddingVector = Convert(part.Embedding);
            }

            await context.SaveChangesAsync(cancellationToken);

            filled += parts.Count;
        }

        if (filled > 0)
        {
            logger.LogInformation(
                "Векторы частей перенесены в pgvector: {Count} шт. Переразбор методичек не потребовался",
                filled);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static Vector? Convert(byte[] embedding)
    {
        if (embedding.Length == 0) return null;

        var values = EmbeddingVector.FromBytes(embedding);

        return values.Length == 0 ? null : new Vector(values);
    }
}
