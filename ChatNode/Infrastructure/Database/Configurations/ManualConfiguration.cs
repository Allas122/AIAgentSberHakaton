using ChatNode.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatNode.Infrastructure.Database.Configurations;

public class ManualConfiguration : IEntityTypeConfiguration<StoredManual>
{
    public const int MaxTitleLength = 500;
    public const int MaxStatusDetailLength = 4000;

    public void Configure(EntityTypeBuilder<StoredManual> builder)
    {
        builder.ToTable("manuals");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).IsRequired().HasMaxLength(MaxTitleLength);
        builder.Property(x => x.Navigation).IsRequired();
        builder.Property(x => x.Scope).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.Stage).IsRequired().HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(x => x.Scope);
        builder.Property(x => x.TotalChunks).IsRequired();
        builder.Property(x => x.ProcessedChunks).IsRequired();
        builder.Property(x => x.FailedChunks).IsRequired();
        builder.Property(x => x.StatusDetail).HasMaxLength(MaxStatusDetailLength);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasMany(x => x.Parts)
            .WithOne(x => x.Manual)
            .HasForeignKey(x => x.ManualId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ManualPartConfiguration : IEntityTypeConfiguration<StoredManualPart>
{
    public const int EmbeddingDimensions = 1024;

    public void Configure(EntityTypeBuilder<StoredManualPart> builder)
    {
        builder.ToTable("manual_parts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).IsRequired().HasMaxLength(ManualConfiguration.MaxTitleLength);
        builder.Property(x => x.Navigation).IsRequired();
        builder.Property(x => x.Content).IsRequired();
        builder.Property(x => x.Embedding).IsRequired().HasColumnType("bytea");
        builder.Property(x => x.EmbeddingVector).HasColumnType($"vector({EmbeddingDimensions})");
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.ManualId);
    }
}
