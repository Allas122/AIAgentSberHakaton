using ChatNode.Infrastructure.Dto;

namespace ChatNode.Infrastructure.Tools.Abstractions;

public interface IDocxTextExtractor
{
    IReadOnlyList<ApplicationSectionDto> ExtractSections(Stream docxStream);

    IReadOnlyList<string> ExtractLines(Stream docxStream);
}
