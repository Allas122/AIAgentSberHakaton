using ChatNode.Application.DTO;
using Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace ChatNode.Application.Mappers;

[Mapper]
public static partial class ManualMapper
{
    [MapperIgnoreSource(nameof(Manual.Navigation))]
    [MapProperty(nameof(Manual.StatusDetail), nameof(ManualDto.Detail))]
    public static partial ManualDto MapToManualDto(this Manual manual);
}
