namespace ChatNode.Infrastructure.Dto;

public record ManualPartDto(Guid Id, Guid ManualId, string Title, string Content, string Navigation);