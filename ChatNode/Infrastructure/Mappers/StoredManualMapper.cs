using ChatNode.Infrastructure.Database.Entities;
using Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace ChatNode.Infrastructure.Mappers;

[Mapper]
public static partial class StoredManualMapper
{
    [MapperIgnoreSource(nameof(StoredManual.Parts))]
    [MapperIgnoreSource(nameof(StoredManual.CreatedAt))]
    [MapperIgnoreSource(nameof(StoredManual.UpdatedAt))]
    public static partial Manual MapToManual(this StoredManual stored);

    [MapperIgnoreSource(nameof(StoredManualPart.Manual))]
    [MapperIgnoreSource(nameof(StoredManualPart.Embedding))]
    [MapperIgnoreSource(nameof(StoredManualPart.EmbeddingVector))]
    [MapperIgnoreSource(nameof(StoredManualPart.CreatedAt))]
    public static partial ManualPart MapToManualPart(this StoredManualPart stored);
}
