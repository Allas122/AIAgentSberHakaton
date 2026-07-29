using Domain.Entities;
using Domain.ValueTypes;

namespace ChatNode.Application.DTO;

public record MessageDisplayDto(string Id, string Content, UserRole UserRole, string userName);