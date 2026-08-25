using ChatNode.Infrastructure.Database;
using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ChatNode.Infrastructure.Repositories;

public class LetterTemplateRepository(AppDbContext context) : ILetterTemplateRepository
{
    public async Task<IReadOnlyList<LetterTemplate>> ListAsync(Guid ownerId) =>
        await context.LetterTemplates
            .AsNoTracking()
            .Where(x => x.OwnerId == ownerId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

    public async Task<LetterTemplate?> GetAsync(Guid templateId) =>
        await context.LetterTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == templateId);

    public async Task<bool> NameTakenAsync(Guid ownerId, string name, Guid? exceptId = null) =>
        await context.LetterTemplates.AnyAsync(x =>
            x.OwnerId == ownerId &&
            x.Name.ToLower() == name.ToLower() &&
            (exceptId == null || x.Id != exceptId));

    public async Task CreateAsync(LetterTemplate template)
    {
        context.LetterTemplates.Add(template);
        await context.SaveChangesAsync();
    }

    public async Task<bool> UpdateAsync(LetterTemplate template)
    {
        var stored = await context.LetterTemplates.FirstOrDefaultAsync(x => x.Id == template.Id);
        if (stored is null) return false;

        stored.Name = template.Name;
        stored.Content = template.Content;
        stored.SourceFileName = template.SourceFileName;
        stored.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(Guid templateId) =>
        await context.LetterTemplates.Where(x => x.Id == templateId).ExecuteDeleteAsync() > 0;
}
