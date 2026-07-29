using ChatNode.Infrastructure.AI.Agents;
using ChatNode.Infrastructure.AI.Policy;
using ChatNode.Infrastructure.AI.Services;
using ChatNode.Infrastructure.AI.Services.Abstractions;
using ChatNode.Infrastructure.Configuration.Options;
using GigaChat.Net;
using GigaChat.Net.AspNetCore;
using Microsoft.Extensions.Options;
namespace ChatNode.Infrastructure.AI.Configuration;

public static class AiConfigurationExtension
{
    public static void AddGigaChatInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient();

        services.AddHttpClient("RussianCerts")
            .ConfigurePrimaryHttpMessageHandler(() => GigaChatCertificateHelper.CreateHandlerWithRussianCerts())
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));
        
        services.AddTransient<IGigaChatClient>(sp =>
        {
            var myOptions = sp.GetRequiredService<IOptions<ChatNode.Infrastructure.Configuration.Options.GigaChatOptions>>().Value;
            var settings = new Settings()
            {
                Credentials = myOptions.AuthorizationKey,
                Scope =  myOptions.Scope,
                BaseUrl = myOptions.BaseUrl,
                AuthUrl = myOptions.AuthUrl,
                VerifySslCerts =  true,
                Timeout = myOptions.TimeoutMinutes * 60,
            };
            return new GigaChatClient(settings, GigaChatCertificateHelper.CreateHandlerWithRussianCerts(),GigaChatCertificateHelper.CreateHandlerWithRussianCerts());
        });
        services.AddTransient<AgentFactory>();
        
        services.AddScoped<IEmbeddingClient, EmbeddingClient>();
        
    }
}