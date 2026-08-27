using ChatNode.Infrastructure.Configuration.Options;

namespace ChatNode.Infrastructure.Configuration;

public static class ConfigurationInjector
{
    public static IServiceCollection AddMyConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ExpirationPolicyOption>(configuration.GetSection("ExpirationPolicy"));
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<StaffAccountsOptions>(configuration.GetSection("StaffAccounts"));
        services.Configure<GigaChatOptions>(configuration.GetSection("GigaChat"));
        services.Configure<WebSocketOption>(configuration.GetSection("WebSocket"));
        services.Configure<S3Options>(configuration.GetSection("S3"));
        services.Configure<ManualUploadOptions>(configuration.GetSection("ManualUpload"));
        services.Configure<CachePolicyOption>(configuration.GetSection("CachePolicy"));
        services.Configure<AnonymizerOptions>(configuration.GetSection("Anonymizer"));
        return services;
    }
}