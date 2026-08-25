using System.Globalization;
using Domain.Entities;
using Domain.ValueTypes;
using StackExchange.Redis;

namespace ChatNode.Infrastructure.Mappers;

public static class ValkeyChatMapper
{
    public static Chat ToChat(this HashEntry[] entries, Guid id)
    {
        var dict = entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());

        return new Chat(
            id,
            Guid.Parse(dict["UserId"]),
            dict["Title"],
            dict.TryGetValue("Kind", out var kind) && Enum.TryParse<ChatKind>(kind, out var parsed)
                ? parsed
                : ChatKind.Grant
        );
    }

    public static HashEntry[] ToHashEntries(this Chat chat)
    {
        return new HashEntry[]
        {
            new("UserId", chat.UserId.ToString()),
            new("Title", chat.Title),
            new("Kind", chat.Kind.ToString())
        };
    }

    public static Message ToMessage(this StreamEntry entry)
    {
        var dict = entry.Values.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());

        return new Message(
            Id: entry.Id.ToString(),
            Content: dict["Content"],
            SenderId: Guid.Parse(dict["SenderId"]),
            CreateAt: DateTime.Parse(dict["CreateAt"], null, DateTimeStyles.RoundtripKind),
            DocumentId: dict.TryGetValue("DocumentId", out var docId) && Guid.TryParse(docId, out var guid)
                ? guid
                : null,
            FileName: dict.GetValueOrDefault("FileName")
        );
    }

    public static Message ToMessage(this RedisResult result)
    {
        RedisResult[]? entryArray = (RedisResult[]?)result;
        if (entryArray is null || entryArray.Length < 2)
            throw new InvalidOperationException("Запись потока сообщений пуста или неполна.");

        var streamId = entryArray[0].ToString();

        RedisResult[]? fieldArray = (RedisResult[]?)entryArray[1];
        if (fieldArray is null)
            throw new InvalidOperationException($"У записи потока {streamId} нет полей.");

        var dict = new Dictionary<string, string?>();
        for (int i = 0; i < fieldArray.Length; i += 2)
        {
            dict[fieldArray[i].ToString()!] = fieldArray[i + 1].ToString();
        }

        return new Message(
            Id: streamId!,
            Content: dict["Content"] ?? string.Empty,
            SenderId: Guid.Parse(dict["SenderId"]!),
            CreateAt: DateTime.Parse(dict["CreateAt"]!, null, DateTimeStyles.RoundtripKind),
            DocumentId: dict.TryGetValue("DocumentId", out var dId) && Guid.TryParse(dId, out var guid)
                ? guid
                : null,
            FileName: dict.GetValueOrDefault("FileName")
        );
    }

    public static NameValueEntry[] ToStreamEntries(this Message message)
    {
        var entries = new List<NameValueEntry>
        {
            new("Content", message.Content),
            new("SenderId", message.SenderId.ToString()),
            new("CreateAt", message.CreateAt.ToString("O"))
        };

        if (message.DocumentId.HasValue)
        {
            entries.Add(new("DocumentId", message.DocumentId.Value.ToString()));
        }

        if (!string.IsNullOrEmpty(message.FileName))
        {
            entries.Add(new("FileName", message.FileName));
        }

        return entries.ToArray();
    }
}