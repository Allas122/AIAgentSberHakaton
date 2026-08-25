namespace ChatNode.Infrastructure.AI.Agents;

public record ManualParseProgress(int TotalChunks, int ProcessedChunks, int FailedChunks);

public record ManualParseGap(string Preview, string Reason);

public record ManualParseReport(int TotalChunks, int ProcessedChunks, IReadOnlyList<ManualParseGap> Gaps)
{
    public bool Complete => Gaps.Count == 0 && ProcessedChunks >= TotalChunks;
}
