using System.Globalization;
using Domain.Entities;
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
            dict["Title"]
        );
    }

    public static HashEntry[] ToHashEntries(this Chat chat)
    {
        return new HashEntry[]
        {
            new("UserId", chat.UserId.ToString()),
            new("Title", chat.Title)
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
            FileId: dict.TryGetValue("FileId", out var fileId) && Guid.TryParse(fileId, out var guid) ? guid : null
        );
    }

    public static Message ToMessage(this RedisResult result)
    {
        var entryArray = (RedisResult[])result;
        var streamId = entryArray[0].ToString();
        var fieldArray = (RedisResult[])entryArray[1];

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
            FileId: dict.TryGetValue("FileId", out var fId) && Guid.TryParse(fId, out var guid) ? guid : null
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

        if (message.FileId.HasValue)
        {
            entries.Add(new("FileId", message.FileId.Value.ToString()));
        }

        return entries.ToArray();
    }
}