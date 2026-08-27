using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatNode.Infrastructure.Database.Configurations;

public class LetterTemplateConfiguration : IEntityTypeConfiguration<LetterTemplate>
{
    public const int MaxNameLength = 200;
    public const int MaxContentLength = 20000;
    public const int MaxFileNameLength = 300;
    public const int MaxStorageKeyLength = 300;
    public const int MaxPlaceholderListLength = 1000;

    public void Configure(EntityTypeBuilder<LetterTemplate> builder)
    {
        builder.ToTable("letter_templates");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(MaxNameLength);
        builder.Property(x => x.Content).IsRequired().HasMaxLength(MaxContentLength);
        builder.Property(x => x.SourceFileName).HasMaxLength(MaxFileNameLength);
        builder.Property(x => x.FormStorageKey).HasMaxLength(MaxStorageKeyLength);
        builder.Property(x => x.FormFileName).HasMaxLength(MaxFileNameLength);
        builder.Property(x => x.FormPlaceholders).HasMaxLength(MaxPlaceholderListLength);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => new { x.OwnerId, x.CreatedAt });
    }
}
