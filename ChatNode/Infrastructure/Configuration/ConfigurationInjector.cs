using ChatNode.Infrastructure.Configuration.Options;

namespace ChatNode.Infrastructure.Configuration;

public static class ConfigurationInjector
{
    public static IServiceCollection AddMyConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ExpirationPolicyOption>(configuration.GetSection("ExpirationPolicy"));
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<GigaChatOptions>(configuration.GetSection("GigaChat"));
        services.Configure<WebSocketOption>(configuration.GetSection("WebSocket"));
        services.Configure<S3Options>(configuration.GetSection("S3"));
        return services;
    }
}