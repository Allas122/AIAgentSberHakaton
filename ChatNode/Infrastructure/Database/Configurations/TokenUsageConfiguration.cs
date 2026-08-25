using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatNode.Infrastructure.Database.Configurations;

public class TokenUsageConfiguration : IEntityTypeConfiguration<TokenUsage>
{
    public const int MaxModelLength = 100;

    public void Configure(EntityTypeBuilder<TokenUsage> builder)
    {
        builder.ToTable("token_usage");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Operation).HasConversion<int>();
        builder.Property(x => x.Model).IsRequired().HasMaxLength(MaxModelLength);
        builder.Property(x => x.PromptTokens).IsRequired();
        builder.Property(x => x.CompletionTokens).IsRequired();
        builder.Property(x => x.TotalTokens).IsRequired();
        builder.Property(x => x.ToolCalls).IsRequired();
        builder.Property(x => x.Partial).IsRequired();
        builder.Property(x => x.Failed).IsRequired();
        builder.Property(x => x.StartedAt).IsRequired();
        builder.Property(x => x.Duration).IsRequired();

        builder.HasIndex(x => x.StartedAt);
        builder.HasIndex(x => new { x.Operation, x.StartedAt });
    }
}
