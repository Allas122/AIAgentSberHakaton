using Domain.ValueTypes;

namespace ChatNode.Infrastructure.Dto;


public class MessageHistoricalDto
{
    public string Content { get; set; }
    public UserRole SenderRole { get; set; }
}