using System.Text.Encodings.Web;
using System.Text.Json;
using ChatNode.Infrastructure.Database.Entities;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatNode.Infrastructure.Database.Configurations;

public class DatasetConfiguration : IEntityTypeConfiguration<Dataset>
{
    public const int MaxTitleLength = 500;
    public const int MaxColumnNameLength = 300;

    public void Configure(EntityTypeBuilder<Dataset> builder)
    {
        builder.ToTable("datasets");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).IsRequired().HasMaxLength(MaxTitleLength);
        builder.Property(x => x.SheetName).IsRequired().HasMaxLength(MaxTitleLength);
        builder.Property(x => x.RowCount).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.ManualId);
        builder.HasIndex(x => x.OwnerId);

        builder.HasOne<StoredManual>()
            .WithMany()
            .HasForeignKey(x => x.ManualId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Columns)
            .WithOne()
            .HasForeignKey(x => x.DatasetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Rows)
            .WithOne()
            .HasForeignKey(x => x.DatasetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DatasetColumnConfiguration : IEntityTypeConfiguration<DatasetColumn>
{
    public void Configure(EntityTypeBuilder<DatasetColumn> builder)
    {
        builder.ToTable("dataset_columns");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(DatasetConfiguration.MaxColumnNameLength);
        builder.Property(x => x.Ordinal).IsRequired();
        builder.Property(x => x.Kind).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(x => x.DatasetId);
    }
}

public class DatasetRowConfiguration : IEntityTypeConfiguration<DatasetRow>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly ValueComparer<Dictionary<string, string>> ValuesComparer = new(
        (left, right) => left != null && right != null && left.Count == right.Count && !left.Except(right).Any(),
        values => values.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value)),
        values => new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase));

    public void Configure(EntityTypeBuilder<DatasetRow> builder)
    {
        builder.ToTable("dataset_rows");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Ordinal).IsRequired();

        builder.Property(x => x.Values)
            .HasColumnType("jsonb")
            .IsRequired()
            .HasConversion(
                values => JsonSerializer.Serialize(values, JsonOptions),
                json => JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
                        ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                ValuesComparer);

        builder.HasIndex(x => x.DatasetId);
    }
}
