namespace ChatNode.Infrastructure.Configuration.Options;

public record GigaChatOptions
{
    public string AuthorizationKey { get; init; } = string.Empty;
    public string AuthUrl { get; init; } = "https://ngw.devices.sberbank.ru/api/v2/oauth";
    public string BaseUrl { get; init; } = "https://gigachat.devices.sberbank.ru/api/v1/";
    public string EmbeddingModel { get; init; } = "Embeddings";
    public int EmbeddingDim { get; init; } = 1024;
    public string PrepAgentModel {get; init; }
    public string ConsultingAgentModel {get; init; }
    public string Scope { get; init; } = "GIGACHAT_API_PERS";
    public double TimeoutMinutes { get; init; } = 30;
}