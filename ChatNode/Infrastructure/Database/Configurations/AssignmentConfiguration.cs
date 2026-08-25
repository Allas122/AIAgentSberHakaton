using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatNode.Infrastructure.Database.Configurations;

public class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public const int MaxTitleLength = 300;
    public const int MaxDescriptionLength = 8000;
    public const int MaxAssigneeLength = 200;
    public const int MaxSourceRefLength = 500;

    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("assignments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OwnerId).IsRequired();
        builder.Property(x => x.Title).IsRequired().HasMaxLength(MaxTitleLength);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(MaxDescriptionLength);
        builder.Property(x => x.Assignee).HasMaxLength(MaxAssigneeLength);
        builder.Property(x => x.AssigneeId);
        builder.Property(x => x.SourceRef).HasMaxLength(MaxSourceRefLength);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.SourceKind).HasConversion<int>();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => new { x.OwnerId, x.CreatedAt });
        builder.HasIndex(x => new { x.AssigneeId, x.CreatedAt });
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.Status);
    }
}
