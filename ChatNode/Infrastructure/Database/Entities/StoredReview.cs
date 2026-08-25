using Domain.ValueTypes;

namespace ChatNode.Infrastructure.Database.Entities;

public class StoredReview
{
    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid ManualId { get; set; }
    public Guid? DocumentId { get; set; }
    public string? MessageId { get; set; }
    public string? FileName { get; set; }
    public int TotalScore { get; set; }
    public int MaxScore { get; set; }
    public int UnverifiedCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public List<StoredReviewCriterion> Criteria { get; set; } = [];
}

public class StoredReviewCriterion
{
    public Guid Id { get; set; }
    public Guid ReviewId { get; set; }
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public CriterionStatus Status { get; set; }
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public string Findings { get; set; } = string.Empty;

    public StoredReview? Review { get; set; }
}
