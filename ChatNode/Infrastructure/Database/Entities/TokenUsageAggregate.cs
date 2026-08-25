namespace ChatNode.Infrastructure.Database.Entities;

public class TokenUsageAggregate
{
    public int Operation { get; set; }
    public string Model { get; set; } = string.Empty;
    public long Calls { get; set; }
    public long PartialCalls { get; set; }
    public long FailedCalls { get; set; }
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long TotalTokens { get; set; }
    public double DurationSeconds { get; set; }
    public double SlowestSeconds { get; set; }
    public double BalanceSpent { get; set; }
}
