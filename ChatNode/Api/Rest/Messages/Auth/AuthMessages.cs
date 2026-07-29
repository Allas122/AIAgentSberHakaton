namespace ChatNode.Api.Rest.Messages.Auth;

public record AuthResponse(string AccessToken, string RefreshToken, Guid UserId);

public record RefreshRequest(string AccessToken, string RefreshToken);