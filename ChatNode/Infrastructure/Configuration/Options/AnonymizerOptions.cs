namespace ChatNode.Infrastructure.Configuration.Options;

public class AnonymizerOptions
{
    public int MaxParallelRequests { get; set; } = 4;

    public int MaxRequestChars { get; set; } = 8000;
}
