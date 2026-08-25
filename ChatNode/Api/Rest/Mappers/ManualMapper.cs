using ChatNode.Api.Rest.Messages.Manual;
using ChatNode.Application.DTO;
using Riok.Mapperly.Abstractions;

namespace ChatNode.Api.Rest.Mappers;

[Mapper]
public static partial class ManualMapper
{
    public static partial ManualData MapToManualData(this ManualDto m);

    public static partial UploadManualResponse MapToUploadManualResponse(this ManualUploadDto m);
}
