using System.Text;
using System.Text.RegularExpressions;
using ChatNode.Infrastructure.AI.Generated;
using ChatNode.Infrastructure.AI.Services.Abstractions;
using StackExchange.Redis;

namespace ChatNode.Infrastructure.AI.Services;

public class AnonymizeClient(
    IDatabase redis,
    NerService.NerServiceClient grpcClient,
    ILogger<AnonymizeClient> logger) : IAnonymizeClient
{
    private static readonly Regex AnonTagRegex = AnonymizationTags.Pattern;
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private const string SequenceField = "seq";

    private const char BatchSeparator = '\n';
    private const int MaxBatchChars = 8000;

    public async Task<string> AnonymizeAsync(string text, string sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var request = new NerRequest { Text = text };
        var response = await grpcClient.ExtractEntitiesAsync(request, cancellationToken: ct);

        if (response.Entities.Count == 0) return text;

        var hashKey = $"anon:{sessionId}";
        var sortedEntities = response.Entities.OrderByDescending(e => e.Start).ToList();
        var result = new StringBuilder(text);
        var entries = new HashEntry[sortedEntities.Count];

        var reserved = await redis.HashIncrementAsync(hashKey, SequenceField, sortedEntities.Count);
        var baseIndex = reserved - sortedEntities.Count;

        for (var i = 0; i < sortedEntities.Count; i++)
        {
            var entity = sortedEntities[i];
            var shortId = (baseIndex + i).ToString("X4");
            var fieldName = $"{entity.Type}_{shortId}";
            var pseudonym = $"[{fieldName}]";

            entries[i] = new HashEntry(fieldName, entity.Text);

            result.Remove(entity.Start, entity.End - entity.Start);
            result.Insert(entity.Start, pseudonym);
        }

        var batch = redis.CreateBatch();
        var setTask = batch.HashSetAsync(hashKey, entries);
        var expireTask = batch.KeyExpireAsync(hashKey, Ttl);
        batch.Execute();
        await Task.WhenAll(setTask, expireTask);

        return result.ToString();
    }

    public async Task<IReadOnlyList<string>> AnonymizeBatchAsync(
        IReadOnlyList<string> texts,
        string sessionId,
        CancellationToken ct = default)
    {
        if (texts.Count == 0) return [];

        var result = new string[texts.Count];
        var batchStart = 0;
        var batchChars = 0;

        for (var i = 0; i < texts.Count; i++)
        {
            var length = texts[i].Length + 1;

            if (batchChars > 0 && batchChars + length > MaxBatchChars)
            {
                await FlushAsync(texts, result, batchStart, i, sessionId, ct);
                batchStart = i;
                batchChars = 0;
            }

            batchChars += length;
        }

        await FlushAsync(texts, result, batchStart, texts.Count, sessionId, ct);

        return result;
    }

    private async Task FlushAsync(
        IReadOnlyList<string> texts,
        string[] result,
        int start,
        int end,
        string sessionId,
        CancellationToken ct)
    {
        if (end <= start) return;

        if (end - start == 1)
        {
            result[start] = await AnonymizeAsync(texts[start], sessionId, ct);
            return;
        }

        var slice = new string[end - start];
        for (var i = 0; i < slice.Length; i++) slice[i] = texts[start + i];

        var anonymized = await AnonymizeAsync(string.Join(BatchSeparator, slice), sessionId, ct);
        var parts = anonymized.Split(BatchSeparator);

        if (parts.Length == slice.Length)
        {
            for (var i = 0; i < slice.Length; i++) result[start + i] = parts[i];
            return;
        }

        logger.LogWarning(
            "Анонимизация: батч из {Expected} фрагментов распался на {Actual} — повторяю по одному",
            slice.Length,
            parts.Length);

        for (var i = 0; i < slice.Length; i++)
        {
            result[start + i] = await AnonymizeAsync(slice[i], sessionId, ct);
        }
    }

    private async Task<Dictionary<string, string>> BuildSuffixIndexAsync(string hashKey)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in await redis.HashGetAllAsync(hashKey))
        {
            var name = entry.Name.ToString();
            var separator = name.LastIndexOf('_');

            if (separator <= 0 || separator == name.Length - 1) continue;

            index[name[(separator + 1)..]] = entry.Value.ToString();
        }

        return index;
    }

    public async Task<string> DeanonymizeAsync(string text, string sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var hashKey = $"anon:{sessionId}";
        var matches = AnonTagRegex.Matches(text);
        if (matches.Count == 0) return text;

        var fieldNames = matches
            .Select(m => $"{m.Groups[1].Value}_{m.Groups[2].Value}")
            .Distinct()
            .ToArray();

        var fields = fieldNames.Select(f => (RedisValue)f).ToArray();
        var values = await redis.HashGetAsync(hashKey, fields);

        var lookup = fieldNames
            .Zip(values, (name, val) => (name, val))
            .ToDictionary(x => x.name, x => x.val);

        var bySuffix = lookup.Values.Any(v => !v.HasValue)
            ? await BuildSuffixIndexAsync(hashKey)
            : null;

        var sb = new StringBuilder();
        var lastIndex = 0;
        foreach (Match match in matches)
        {
            sb.Append(text, lastIndex, match.Index - lastIndex);

            var suffix = match.Groups[2].Value;
            var fieldName = $"{match.Groups[1].Value}_{suffix}";

            var original = lookup.TryGetValue(fieldName, out var v) && v.HasValue
                ? v.ToString()
                : bySuffix is not null && bySuffix.TryGetValue(suffix, out var recovered)
                    ? recovered
                    : null;

            if (original is null)
            {
                logger.LogWarning(
                    "Деанонимизация: тег {Tag} не найден в карте сессии {SessionId} — текст останется с тегом",
                    match.Value,
                    sessionId);
            }

            sb.Append(original ?? match.Value);
            lastIndex = match.Index + match.Length;
        }
        sb.Append(text, lastIndex, text.Length - lastIndex);

        return sb.ToString();
    }
}
