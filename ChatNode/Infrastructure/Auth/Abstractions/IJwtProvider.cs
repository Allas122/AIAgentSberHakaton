using System.Security.Claims;
using ChatNode.Infrastructure.Dto;
using Domain.Entities;
using Domain.ValueTypes;

namespace ChatNode.Infrastructure.Auth.Abstractions;

public interface IJwtProvider
{
    public Task<TokenPairDto> GenerateTokenPairAsync(Guid userId, UserRole role, string name);
    public ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
    public Task<TokenPairDto> RefreshTokensAsync(string accessToken, string refreshToken);
}