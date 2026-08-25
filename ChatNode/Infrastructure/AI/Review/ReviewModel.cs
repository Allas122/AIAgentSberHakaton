using Domain.Entities;
using Domain.ValueTypes;

namespace ChatNode.Infrastructure.AI.Review;

public record ReviewFinding(
    string Content,
    PinType Type,
    FindingSource Source,
    FindingScope Scope,
    int? CriterionIndex);

public record CriterionOutcome(
    ReviewCriterion Criterion,
    CriterionStatus Status,
    int Score,
    IReadOnlyList<ReviewFinding> Findings)
{
    public string Explanation { get; set; } = string.Empty;
}

public record ApplicationReviewResult(
    IReadOnlyList<CriterionOutcome> Criteria,
    IReadOnlyList<ReviewFinding> Unassigned,
    IReadOnlyList<string> ComputedDiscrepancies,
    int TotalScore,
    int MaxScore,
    int UnverifiedCount)
{
    public string Summary { get; set; } = string.Empty;
}
