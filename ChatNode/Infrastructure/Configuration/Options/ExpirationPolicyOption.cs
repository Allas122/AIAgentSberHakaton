namespace ChatNode.Infrastructure.Configuration.Options;

public class ExpirationPolicyOption
{
    public int UserExpirationSeconds { get; set; }
    public int ChatExpirationSeconds { get; set; }
    public int MessageStreamExpirationSeconds { get; set; }
}