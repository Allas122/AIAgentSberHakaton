using Domain.Entities;

namespace Domain.Repositories;

public interface ILetterTemplateRepository
{
    Task<IReadOnlyList<LetterTemplate>> ListAsync(Guid ownerId);

    Task<LetterTemplate?> GetAsync(Guid templateId);

    Task<bool> NameTakenAsync(Guid ownerId, string name, Guid? exceptId = null);

    Task CreateAsync(LetterTemplate template);

    Task<bool> UpdateAsync(LetterTemplate template);

    Task<bool> DeleteAsync(Guid templateId);
}
