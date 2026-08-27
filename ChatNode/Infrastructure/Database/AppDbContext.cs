using ChatNode.Infrastructure.Database.Entities;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChatNode.Infrastructure.Database;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Assignment> Assignments => Set<Assignment>();

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    public DbSet<TokenUsage> TokenUsages => Set<TokenUsage>();

    public DbSet<TokenUsageAggregate> TokenUsageAggregates => Set<TokenUsageAggregate>();

    public DbSet<StoredManual> Manuals => Set<StoredManual>();

    public DbSet<StoredManualPart> ManualParts => Set<StoredManualPart>();

    public DbSet<StoredDocument> Documents => Set<StoredDocument>();

    public DbSet<LetterTemplate> LetterTemplates => Set<LetterTemplate>();

    public DbSet<OrganizationProfile> OrganizationProfiles => Set<OrganizationProfile>();

    public DbSet<Dataset> Datasets => Set<Dataset>();

    public DbSet<DatasetColumn> DatasetColumns => Set<DatasetColumn>();

    public DbSet<DatasetRow> DatasetRows => Set<DatasetRow>();

    public DbSet<StoredReview> Reviews => Set<StoredReview>();

    public DbSet<StoredReviewCriterion> ReviewCriteria => Set<StoredReviewCriterion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
