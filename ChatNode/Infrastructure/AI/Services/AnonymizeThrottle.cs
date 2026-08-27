using ChatNode.Infrastructure.Configuration.Options;
using Microsoft.Extensions.Options;

namespace ChatNode.Infrastructure.AI.Services;

public sealed class AnonymizeThrottle : IDisposable
{
    private readonly SemaphoreSlim _gate;

    public AnonymizeThrottle(IOptions<AnonymizerOptions> options)
    {
        var limit = Math.Max(options.Value.MaxParallelRequests, 1);

        _gate = new SemaphoreSlim(limit, limit);
    }

    public async Task<T> RunAsync<T>(Func<Task<T>> call, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);

        try
        {
            return await call();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
