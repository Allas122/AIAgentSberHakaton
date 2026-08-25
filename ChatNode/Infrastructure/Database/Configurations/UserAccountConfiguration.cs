using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatNode.Infrastructure.Database.Configurations;

public class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public const int MaxLoginLength = 100;
    public const int MaxNameLength = 200;
    public const int MaxEmailLength = 320;
    public const int MaxHashLength = 300;

    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("user_accounts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Login).IsRequired().HasMaxLength(MaxLoginLength);
        builder.Property(x => x.PasswordHash).IsRequired().HasMaxLength(MaxHashLength);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(MaxNameLength);
        builder.Property(x => x.Email).HasMaxLength(MaxEmailLength);
        builder.Property(x => x.Role).HasConversion<int>();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.Login).IsUnique();
    }
}
