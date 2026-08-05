using ChatNode.Infrastructure.AI.Agents;
using ChatNode.Infrastructure.AI.Policy;
using ChatNode.Infrastructure.AI.Services;
using ChatNode.Infrastructure.AI.Services.Abstractions;
using ChatNode.Infrastructure.Configuration.Options;
using GigaChat.Net;
using GigaChat.Net.AspNetCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

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

                MaxRetries = myOptions.MaxRetries,
                RetryBackoffFactor = myOptions.RetryBackoffFactor,
                RetryOnStatusCodes = myOptions.RetryOnStatusCodes,
            };
            return new GigaChatClient(
                settings,
                CreateResilientHandler(myOptions),
                CreateResilientHandler(myOptions));
        });
        services.AddTransient<AgentFactory>();

        services.AddScoped<IEmbeddingClient, EmbeddingClient>();
        services.AddScoped<IAnonymizeClient>(sp =>
        {
            var url = configuration.GetConnectionString("AnonimyzeService");

            if (string.IsNullOrEmpty(url))
            {
                throw new InvalidOperationException("Connection string 'AnonimyzeService' is not found.");
            }

            var redis = sp.GetRequiredService<IDatabase>();

            return new AnonymizeClient(redis, url);
        });
    }
    
    private static HttpMessageHandler CreateResilientHandler(
        ChatNode.Infrastructure.Configuration.Options.GigaChatOptions options) =>
        new TransientHttpRetryHandler(options.TransportRetries, options.RetryBackoffFactor)
        {
            InnerHandler = GigaChatCertificateHelper.CreateHandlerWithRussianCerts()
        };
}