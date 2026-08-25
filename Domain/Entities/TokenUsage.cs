using Domain.ValueTypes;

namespace Domain.Entities;

public record TokenUsage
{
    public Guid Id { get; set; }
    public TokenOperation Operation { get; set; }
    public string Model { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public int ToolCalls { get; set; }
    public bool Partial { get; set; }
    public bool Failed { get; set; }
    public double? BalanceSpent { get; set; }
    public Guid? ChatId { get; set; }
    public Guid? UserId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public TimeSpan Duration { get; set; }
}
