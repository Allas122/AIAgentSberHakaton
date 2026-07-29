namespace ChatNode.Infrastructure.Dto;

public record TokenPairDto(string AccessToken, string RefreshToken, string Jti);