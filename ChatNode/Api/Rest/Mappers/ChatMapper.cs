using ChatNode.Api.Rest.Messages.Chat;
using ChatNode.Application.DTO;
using Riok.Mapperly.Abstractions;

namespace ChatNode.Api.Rest.Mappers;

[Mapper]
public static partial class ChatMapper
{
    public static partial Chat MapToChat(this ChatDto dto);
}