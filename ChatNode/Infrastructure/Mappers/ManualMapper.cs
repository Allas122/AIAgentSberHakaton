using ChatNode.Infrastructure.Dto;
using Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace ChatNode.Infrastructure.Mappers;

[Mapper]
public static partial class ManualMapper
{
    public static partial ManualPart MapToManualPart(this ManualPartDto part);
    public static partial ManualPartDto MapToManualPartDto(this ManualPart part);
    public static partial ManualDto MapToManualDto(this Manual part);
    public static partial Manual MapToManual(this ManualDto part);
}