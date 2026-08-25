using ChatNode.Api.WebSockets.Messages;
using ChatNode.Application.DTO;
using Riok.Mapperly.Abstractions;

namespace ChatNode.Api.WebSockets.Mappers;

[Mapper]
public static partial class ChatMapper
{
    public static Chat MapToChat(this ChatDto chat)
    {
        return new Chat(chat.Id, chat.Title, chat.Kind.ToString());
    }
}