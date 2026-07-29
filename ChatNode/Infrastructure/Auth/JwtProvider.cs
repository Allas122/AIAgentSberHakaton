using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ChatNode.Infrastructure.Auth.Abstractions;
using ChatNode.Infrastructure.Configuration.Options;
using ChatNode.Infrastructure.Dto;
using Domain.Entities;
using Domain.ValueTypes;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace ChatNode.Infrastructure.Services;

public class JwtProvider(IOptions<JwtOptions> options, IDatabase database) : IJwtProvider
{
    private readonly JwtOptions _options = options.Value;

    public async Task<TokenPairDto> GenerateTokenPairAsync(Guid userId, UserRole role, string name)
    {
        var jti = Guid.NewGuid().ToString();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var accessClaims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim(ClaimTypes.Role, role.ToString()),
            new Claim("name", name),
            new Claim("token_type", "access")
        };

        var accessTokenObject = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            accessClaims,
            expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenExpirationMinutes),
            signingCredentials: creds);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(accessTokenObject);

        var refreshClaims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim("token_type", "refresh")
        };

        var refreshTokenObject = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            refreshClaims,
            expires: DateTime.UtcNow.AddDays(_options.RefreshTokenExpirationDays),
            signingCredentials: creds);

        var refreshToken = new JwtSecurityTokenHandler().WriteToken(refreshTokenObject);

        var lifeTime = TimeSpan.FromDays(_options.RefreshTokenExpirationDays);
        await database.StringSetAsync($"refresh-token:{jti}", userId.ToString(), lifeTime);

        return new TokenPairDto(accessToken, refreshToken, jti);
    }

    public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = false,
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey))
        };

        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(token, parameters, out var securityToken);

        if (securityToken is not JwtSecurityToken jwtToken ||
            !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            throw new SecurityTokenException("Invalid token");

        return principal;
    }

    public async Task<TokenPairDto> RefreshTokensAsync(string accessToken, string refreshToken)
    {
        var principal = GetPrincipalFromExpiredToken(accessToken);

        var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        var userIdRaw = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var roleRaw = principal.FindFirst(ClaimTypes.Role)?.Value;
        var name = principal.FindFirst("name")?.Value;

        if (string.IsNullOrEmpty(jti) || string.IsNullOrEmpty(userIdRaw))
            throw new SecurityTokenException("Invalid token metadata: JTI or UserId is missing");

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey))
        };

        var handler = new JwtSecurityTokenHandler();
        try
        {
            var refreshPrincipal = handler.ValidateToken(refreshToken, validationParameters, out var validatedToken);

            var tokenType = refreshPrincipal.FindFirst("token_type")?.Value;
            if (tokenType != "refresh") throw new SecurityTokenException("Invalid token type: Expected refresh");

            var refreshJti = refreshPrincipal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            if (jti != refreshJti) throw new SecurityTokenException("Token pair mismatch");
        }
        catch (Exception)
        {
            await database.KeyDeleteAsync($"refresh-token:{jti}");
            throw new SecurityTokenException("Refresh token is invalid or expired");
        }

        var redisKey = $"refresh-token:{jti}";
        var storedUserId = await database.StringGetAsync(redisKey);

        if (storedUserId.IsNull || storedUserId.ToString() != userIdRaw)
            throw new SecurityTokenException("Session not found or revoked");

        await database.KeyDeleteAsync(redisKey);

        if (!Enum.TryParse<UserRole>(roleRaw, out var role)) role = UserRole.Guest;

        return await GenerateTokenPairAsync(
            Guid.Parse(userIdRaw),
            role,
            name ?? "Unknown User"
        );
    }
}