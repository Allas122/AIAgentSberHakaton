using GigaChat.Net;

namespace ChatNode.Infrastructure.AI.Metering;

public class BalanceProbe(IGigaChatClient gigaChatClient, ILogger<BalanceProbe> logger)
{
    private bool _unavailable;

    public async Task<IReadOnlyDictionary<string, double>?> SnapshotAsync(CancellationToken ct)
    {
        if (_unavailable) return null;

        try
        {
            var balance = await gigaChatClient.GetBalanceAsync(ct);

            return balance.BalanceEntries.ToDictionary(entry => entry.Usage, entry => entry.Value);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _unavailable = true;
            logger.LogInformation(
                ex,
                "Баланс GigaChat недоступен, сверка расхода по нему отключена до перезапуска");

            return null;
        }
    }

    public static IReadOnlyDictionary<string, double> Spent(
        IReadOnlyDictionary<string, double>? before,
        IReadOnlyDictionary<string, double>? after)
    {
        var spent = new Dictionary<string, double>();

        if (before is null || after is null) return spent;

        foreach (var (model, start) in before)
        {
            if (!after.TryGetValue(model, out var end)) continue;

            var diff = start - end;
            if (diff > 0) spent[model] = diff;
        }

        return spent;
    }
}
