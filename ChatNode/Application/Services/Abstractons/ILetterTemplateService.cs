using ChatNode.Application.DTO;

namespace ChatNode.Application.Services.Abstractons;

public interface ILetterTemplateService
{
    Task<IReadOnlyList<LetterTemplateDto>> ListAsync(Guid ownerId);

    Task<LetterTemplateDto> CreateAsync(Guid ownerId, CreateLetterTemplateDto dto, CancellationToken ct = default);

    Task<LetterTemplateDto> UpdateAsync(Guid ownerId, Guid templateId, UpdateLetterTemplateDto dto);

    Task DeleteAsync(Guid ownerId, Guid templateId);
}
