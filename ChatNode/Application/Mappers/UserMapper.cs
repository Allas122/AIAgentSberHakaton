using ChatNode.Application.DTO;
using Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace ChatNode.Application.Mappers;

[Mapper]
public static partial class UserMapper
{
    public static partial UserDto MapToUserDto(this User user);
}