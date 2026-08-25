using ChatNode.Infrastructure.Dto;
using Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace ChatNode.Infrastructure.Mappers;

[Mapper]
public static partial class ManualMapper
{
    public static partial ManualPart MapToManualPart(this ManualPartDto part);
    public static partial ManualPartDto MapToManualPartDto(this ManualPart part);
    [MapperIgnoreSource(nameof(Manual.Stage))]
    [MapperIgnoreSource(nameof(Manual.TotalChunks))]
    [MapperIgnoreSource(nameof(Manual.ProcessedChunks))]
    [MapperIgnoreSource(nameof(Manual.FailedChunks))]
    [MapperIgnoreSource(nameof(Manual.StatusDetail))]
    public static partial ManualDto MapToManualDto(this Manual part);

    [MapperIgnoreTarget(nameof(Manual.Stage))]
    [MapperIgnoreTarget(nameof(Manual.TotalChunks))]
    [MapperIgnoreTarget(nameof(Manual.ProcessedChunks))]
    [MapperIgnoreTarget(nameof(Manual.FailedChunks))]
    [MapperIgnoreTarget(nameof(Manual.StatusDetail))]
    public static partial Manual MapToManual(this ManualDto part);
}