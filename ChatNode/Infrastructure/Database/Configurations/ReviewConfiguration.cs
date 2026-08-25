using ChatNode.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatNode.Infrastructure.Database.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<StoredReview>
{
    public const int MaxFileNameLength = 300;
    public const int MaxMessageIdLength = 64;

    public void Configure(EntityTypeBuilder<StoredReview> builder)
    {
        builder.ToTable("application_reviews");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName).HasMaxLength(MaxFileNameLength);
        builder.Property(x => x.MessageId).HasMaxLength(MaxMessageIdLength);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.ChatId, x.CreatedAt });
        builder.HasIndex(x => new { x.OwnerId, x.CreatedAt });
        builder.HasIndex(x => x.DocumentId);

        builder.HasMany(x => x.Criteria)
            .WithOne(x => x.Review)
            .HasForeignKey(x => x.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ReviewCriterionConfiguration : IEntityTypeConfiguration<StoredReviewCriterion>
{
    public const int MaxNameLength = 200;

    public void Configure(EntityTypeBuilder<StoredReviewCriterion> builder)
    {
        builder.ToTable("application_review_criteria");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(MaxNameLength);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Explanation).IsRequired();
        builder.Property(x => x.Findings).IsRequired();

        builder.HasIndex(x => x.ReviewId);
    }
}
