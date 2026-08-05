using System.Text;
using System.Text.RegularExpressions;
using ChatNode.Infrastructure.AI.Generated;
using ChatNode.Infrastructure.AI.Services.Abstractions;
using Grpc.Net.Client;
using StackExchange.Redis;

namespace ChatNode.Infrastructure.AI.Services;

public class AnonymizeClient : IAnonymizeClient
{
    private readonly NerService.NerServiceClient _grpcClient;
    private readonly IDatabase _redis;
    private static readonly Regex AnonTagRegex = new(@"\[([A-Z][A-Z_]*)_([0-9A-F]+)\]", RegexOptions.Compiled);
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    public AnonymizeClient(IDatabase redis, string aiServiceUrl)
    {
        _redis = redis;
        var channel = GrpcChannel.ForAddress(aiServiceUrl);
        _grpcClient = new NerService.NerServiceClient(channel);
    }

    public async Task<string> AnonymizeAsync(string text, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var request = new NerRequest { Text = text };
        var response = await _grpcClient.ExtractEntitiesAsync(request);

        if (response.Entities.Count == 0) return text;

        var hashKey = $"anon:{sessionId}";
        var sortedEntities = response.Entities.OrderByDescending(e => e.Start).ToList();
        var result = new StringBuilder(text);
        var entries = new HashEntry[sortedEntities.Count];

        var baseIndex = (int)await _redis.HashLengthAsync(hashKey);

        for (int i = 0; i < sortedEntities.Count; i++)
        {
            var entity = sortedEntities[i];
            string shortId = (baseIndex + i).ToString("X4");
            string fieldName = $"{entity.Type}_{shortId}";
            string pseudonym = $"[{fieldName}]";

            entries[i] = new HashEntry(fieldName, entity.Text);

            result.Remove(entity.Start, entity.End - entity.Start);
            result.Insert(entity.Start, pseudonym);
        }
        var batch = _redis.CreateBatch();
        var setTask = batch.HashSetAsync(hashKey, entries);
        var expireTask = batch.KeyExpireAsync(hashKey, Ttl);
        batch.Execute();
        await Task.WhenAll(setTask, expireTask);

        return result.ToString();
    }

    public async Task<string> DeanonymizeAsync(string text, string sessionId)
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
        var values = await _redis.HashGetAsync(hashKey, fields);

        var lookup = fieldNames
            .Zip(values, (name, val) => (name, val))
            .ToDictionary(x => x.name, x => x.val);

        var sb = new StringBuilder();
        var lastIndex = 0;
        foreach (Match match in matches)
        {
            sb.Append(text, lastIndex, match.Index - lastIndex);
            var fieldName = $"{match.Groups[1].Value}_{match.Groups[2].Value}";
            var original = lookup.TryGetValue(fieldName, out var v) ? v : RedisValue.Null;
            sb.Append(original.HasValue ? original.ToString() : match.Value);
            lastIndex = match.Index + match.Length;
        }
        sb.Append(text, lastIndex, text.Length - lastIndex);

        return sb.ToString();
    }
}