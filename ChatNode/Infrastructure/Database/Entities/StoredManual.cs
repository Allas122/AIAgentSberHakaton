using Domain.ValueTypes;

namespace ChatNode.Infrastructure.Database.Entities;

public class StoredManual
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Navigation { get; set; } = string.Empty;
    public ManualStage Stage { get; set; }
    public int TotalChunks { get; set; }
    public int ProcessedChunks { get; set; }
    public int FailedChunks { get; set; }
    public string? StatusDetail { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<StoredManualPart> Parts { get; set; } = [];
}

public class StoredManualPart
{
    public Guid Id { get; set; }
    public Guid ManualId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Navigation { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public byte[] Embedding { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }

    public StoredManual? Manual { get; set; }
}
