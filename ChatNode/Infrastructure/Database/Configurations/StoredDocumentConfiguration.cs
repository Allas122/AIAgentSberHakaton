using ChatNode.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatNode.Infrastructure.Database.Configurations;

public class StoredDocumentConfiguration : IEntityTypeConfiguration<StoredDocument>
{
    public const int MaxStorageKeyLength = 500;
    public const int MaxFileNameLength = 300;

    public void Configure(EntityTypeBuilder<StoredDocument> builder)
    {
        builder.ToTable("stored_documents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StorageKey).IsRequired().HasMaxLength(MaxStorageKeyLength);
        builder.Property(x => x.FileName).HasMaxLength(MaxFileNameLength);
        builder.Property(x => x.Kind).HasConversion<int>();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.HasIndex(x => new { x.OwnerId, x.CreatedAt });
        builder.HasIndex(x => x.ChatId);
    }
}
