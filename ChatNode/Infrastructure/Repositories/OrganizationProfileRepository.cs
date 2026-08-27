using ChatNode.Infrastructure.Database;
using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ChatNode.Infrastructure.Repositories;

public class OrganizationProfileRepository(AppDbContext context) : IOrganizationProfileRepository
{
    public async Task<OrganizationProfile?> GetAsync(Guid ownerId, CancellationToken ct = default) =>
        await context.OrganizationProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OwnerId == ownerId, ct);

    public async Task SaveAsync(OrganizationProfile profile, CancellationToken ct = default)
    {
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        var stored = await context.OrganizationProfiles
            .FirstOrDefaultAsync(x => x.OwnerId == profile.OwnerId, ct);

        if (stored is null)
        {
            context.OrganizationProfiles.Add(profile);
        }
        else
        {
            context.Entry(stored).CurrentValues.SetValues(profile);
        }

        await context.SaveChangesAsync(ct);
    }
}
