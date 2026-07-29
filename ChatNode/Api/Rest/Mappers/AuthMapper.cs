using ChatNode.Api.Rest.Messages.Auth;
using ChatNode.Infrastructure.Dto;
using Riok.Mapperly.Abstractions;

namespace ChatNode.Api.Rest.Mappers;

[Mapper]
public static partial class AuthMapper
{
    public static TokenPairDto MapToTokenPair(this RefreshRequest request)
    {
        return new TokenPairDto(request.AccessToken, request.RefreshToken, null);
    }
}