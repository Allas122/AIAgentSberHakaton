using ChatNode.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatNode.Infrastructure.Database.Configurations;

public class TokenUsageAggregateConfiguration : IEntityTypeConfiguration<TokenUsageAggregate>
{
    public void Configure(EntityTypeBuilder<TokenUsageAggregate> builder)
    {
        builder.HasNoKey().ToView(null);
    }
}
