using ChatNode.Infrastructure.AI.Agents;
using ChatNode.Infrastructure.AI.Generated;
using ChatNode.Infrastructure.AI.Policy;
using ChatNode.Infrastructure.AI.Services;
using ChatNode.Infrastructure.AI.Services.Abstractions;
using GigaChat.Net;
using Grpc.Net.Client;
using Microsoft.Extensions.Options;
using GigaChatOptions = ChatNode.Infrastructure.Configuration.Options.GigaChatOptions;

namespace ChatNode.Infrastructure.AI.Configuration;

public static class AiConfigurationExtension
{
    private const string GigaChatHttpClientName = "GigaChat";

    public static void AddGigaChatInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<GigaChatCertificateHelper>();

        services.AddHttpClient(GigaChatHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(sp =>
                sp.GetRequiredService<GigaChatCertificateHelper>().CreateHandlerWithRussianCerts())
            .AddHttpMessageHandler(sp =>
            {
                var options = sp.GetRequiredService<IOptions<GigaChatOptions>>().Value;

                return new TransientHttpRetryHandler(
                    options.TransportRetries,
                    options.RetryBackoffFactor,
                    sp.GetRequiredService<ILogger<TransientHttpRetryHandler>>());
            })
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

        services.AddSingleton<IGigaChatClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GigaChatOptions>>().Value;
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();

            var settings = new Settings
            {
                Credentials = options.AuthorizationKey,
                Scope = options.Scope,
                BaseUrl = options.BaseUrl,
                AuthUrl = options.AuthUrl,
                VerifySslCerts = true,
                Timeout = options.TimeoutMinutes * 60,

                MaxRetries = options.MaxRetries,
                RetryBackoffFactor = options.RetryBackoffFactor,
                RetryOnStatusCodes = options.RetryOnStatusCodes,
            };

            return GigaChatClient.CreateWithHttpClient(
                settings,
                httpClientFactory.CreateClient(GigaChatHttpClientName),
                httpClientFactory.CreateClient(GigaChatHttpClientName));
        });

        services.AddTransient<AgentFactory>();

        services.AddScoped<IEmbeddingClient, EmbeddingClient>();

        services.AddSingleton(_ =>
        {
            var url = configuration.GetConnectionString("AnonimyzeService");

            if (string.IsNullOrEmpty(url))
            {
                throw new InvalidOperationException("Connection string 'AnonimyzeService' is not found.");
            }

            return GrpcChannel.ForAddress(url);
        });

        services.AddSingleton(sp => new NerService.NerServiceClient(sp.GetRequiredService<GrpcChannel>()));

        services.AddScoped<IAnonymizeClient, AnonymizeClient>();
    }
}
