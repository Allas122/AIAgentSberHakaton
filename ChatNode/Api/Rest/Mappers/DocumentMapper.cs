using ChatNode.Api.Rest.Messages.Documents;
using ChatNode.Application.DTO;

namespace ChatNode.Api.Rest.Mappers;

public static class DocumentMapper
{
    public static DocumentResponse MapToResponse(this DocumentDto dto) =>
        new(dto.Id,
            dto.Kind.ToString(),
            dto.FileName,
            dto.SizeBytes,
            dto.OwnerId,
            dto.ChatId,
            dto.ContainsPersonalData,
            dto.CreatedAt,
            dto.Review?.MapToResponse());

    public static DocumentReviewResponse MapToResponse(this DocumentReviewDto dto) =>
        new(dto.Stage,
            dto.Detail,
            dto.ChatId,
            dto.MessageId,
            dto.TotalScore,
            dto.MaxScore,
            dto.UnverifiedCount,
            dto.CompletedAt);

    public static DeleteDocumentResponse MapToResponse(this DocumentDeletionDto dto) =>
        new(dto.DocumentId,
            dto.Kind.ToString(),
            dto.FileName,
            dto.ManualRemoved,
            dto.Message);

    public static DocumentListResponse MapToResponse(this DocumentPageDto page) =>
        new(page.Items.Select(MapToResponse).ToList(), page.Total);
}
