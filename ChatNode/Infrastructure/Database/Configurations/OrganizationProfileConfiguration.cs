using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatNode.Infrastructure.Database.Configurations;

public class OrganizationProfileConfiguration : IEntityTypeConfiguration<OrganizationProfile>
{
    public const int MaxNameLength = 500;
    public const int MaxLineLength = 300;
    public const int MaxCodeLength = 30;

    public void Configure(EntityTypeBuilder<OrganizationProfile> builder)
    {
        builder.ToTable("organization_profiles");

        builder.HasKey(x => x.OwnerId);

        builder.Property(x => x.FullName).IsRequired().HasMaxLength(MaxNameLength);
        builder.Property(x => x.ShortName).IsRequired().HasMaxLength(MaxNameLength);
        builder.Property(x => x.Address).IsRequired().HasMaxLength(MaxLineLength);
        builder.Property(x => x.Phone).IsRequired().HasMaxLength(MaxLineLength);
        builder.Property(x => x.Fax).IsRequired().HasMaxLength(MaxLineLength);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(MaxLineLength);
        builder.Property(x => x.Website).IsRequired().HasMaxLength(MaxLineLength);

        builder.Property(x => x.Okpo).IsRequired().HasMaxLength(MaxCodeLength);
        builder.Property(x => x.Ogrn).IsRequired().HasMaxLength(MaxCodeLength);
        builder.Property(x => x.Inn).IsRequired().HasMaxLength(MaxCodeLength);
        builder.Property(x => x.Kpp).IsRequired().HasMaxLength(MaxCodeLength);

        builder.Property(x => x.SignerPosition).IsRequired().HasMaxLength(MaxLineLength);
        builder.Property(x => x.SignerName).IsRequired().HasMaxLength(MaxLineLength);

        builder.Property(x => x.ContactName).IsRequired().HasMaxLength(MaxLineLength);
        builder.Property(x => x.ContactPosition).IsRequired().HasMaxLength(MaxLineLength);
        builder.Property(x => x.ContactPhone).IsRequired().HasMaxLength(MaxLineLength);
        builder.Property(x => x.ContactEmail).IsRequired().HasMaxLength(MaxLineLength);
        builder.Property(x => x.ExecutorName).IsRequired().HasMaxLength(MaxLineLength);
        builder.Property(x => x.ExecutorPhone).IsRequired().HasMaxLength(MaxLineLength);

        builder.Property(x => x.UpdatedAt).IsRequired();
    }
}
