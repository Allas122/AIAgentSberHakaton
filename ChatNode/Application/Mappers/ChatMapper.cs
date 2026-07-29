using ChatNode.Application.DTO;
using ChatNode.Infrastructure.Dto;
using Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace ChatNode.Application.Mappers;

[Mapper]
public static partial class ChatMapper
{
    public static partial ChatDto MapToChatDto(this Chat dto);
    public static partial Chat MapToChat(this ChatDto dto);
    public static partial MessageDto MapToMassageDto(this Message message);
    public static partial Message MapToMassage(this MessageDto message);
    

}