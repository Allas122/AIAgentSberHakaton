namespace ChatNode.Infrastructure.Configuration.Options;

public record GigaChatOptions
{
    public string AuthorizationKey { get; init; } = string.Empty;
    public string AuthUrl { get; init; } = "https://ngw.devices.sberbank.ru/api/v2/oauth";
    public string BaseUrl { get; init; } = "https://gigachat.devices.sberbank.ru/api/v1/";
    public string EmbeddingModel { get; init; } = "Embeddings";
    public int EmbeddingDim { get; init; } = 1024;
    public string ManualParserAgentModel {get; init; }
    public string ConsultingAgentModel {get; init; }
    public string ApplicationReviewAgentModel {get; init; }
    public int ApplicationSectionChars { get; init; } = 4000;
    public int ApplicationSectionToolCalls { get; init; } = 12;
    public double PinDuplicateDistance { get; init; } = 0.15;
    public double PinSearchDistance { get; init; } = 0.55;
    public string Scope { get; init; } = "GIGACHAT_API_PERS";
    public double TimeoutMinutes { get; init; } = 30;
    
    public int MaxRetries { get; init; } = 4;

    public double RetryBackoffFactor { get; init; } = 1.0;

    public IReadOnlyList<int> RetryOnStatusCodes { get; init; } = [408, 429, 500, 502, 503, 504];

    public int TransportRetries { get; init; } = 3;

    public int MaxOperationAttempts { get; init; } = 2;

    public double OperationRetryDelaySeconds { get; init; } = 5.0;
}