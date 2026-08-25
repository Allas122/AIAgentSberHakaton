namespace ChatNode.Infrastructure.AI.Functions.ReturnModels;

public record ReviewCriterionView(
    int Index,
    string Name,
    string Verdict,
    int Score,
    int MaxScore,
    string Explanation,
    IReadOnlyList<string> Findings);

public record GetReviewReturn(
    string Status,
    string? FileName,
    int TotalScore,
    int MaxScore,
    int UnverifiedCount,
    IReadOnlyList<ReviewCriterionView> Criteria);

public record ReviewActionReturn(string Status, string Message);
