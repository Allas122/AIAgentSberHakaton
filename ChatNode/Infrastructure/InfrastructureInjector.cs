using ChatNode.Infrastructure.AI.Configuration;
using ChatNode.Infrastructure.AI.Metering;
using ChatNode.Infrastructure.Analytics;
using ChatNode.Infrastructure.Auth;
using ChatNode.Infrastructure.Auth.Abstractions;
using ChatNode.Infrastructure.Configuration;
using ChatNode.Infrastructure.Database;
using ChatNode.Infrastructure.Manuals;
using ChatNode.Infrastructure.Repositories;
using ChatNode.Infrastructure.Review;
using ChatNode.Infrastructure.Storage;
using ChatNode.Infrastructure.Tools;
using ChatNode.Infrastructure.Tools.Abstractions;
using ChatNode.Infrastructure.Services;
using Domain.Repositories;
using StackExchange.Redis;

namespace ChatNode.Infrastructure;

public static class InfrastructureInjector
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var valkeyConnectionString = configuration.GetConnectionString("Valkey");

        var redisOptions = ConfigurationOptions.Parse(valkeyConnectionString!);
        redisOptions.Protocol = RedisProtocol.Resp2;
        redisOptions.SyncTimeout = 15_000;
        redisOptions.AsyncTimeout = 15_000;
        redisOptions.KeepAlive = 5;
        redisOptions.ConnectRetry = 5;
        redisOptions.AbortOnConnectFail = false;

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var multiplexer = ConnectionMultiplexer.Connect(redisOptions);
            var logger = sp.GetRequiredService<ILogger<IConnectionMultiplexer>>();

            multiplexer.ConnectionFailed += (_, e) =>
                logger.LogWarning(
                    e.Exception,
                    "Соединение с Valkey оборвалось: {FailureType} на {EndPoint}",
                    e.FailureType,
                    e.EndPoint);

            multiplexer.ConnectionRestored += (_, e) =>
                logger.LogInformation("Соединение с Valkey восстановлено: {EndPoint}", e.EndPoint);

            return multiplexer;
        });
        services.AddScoped<IDatabase>(sp => sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());

        services.AddDatabase(configuration);

        services.AddMyConfiguration(configuration);
        
        services.AddStorage();

        services.AddHostedService<ValkeyIndexInitializer>();
        services.AddHostedService<EmbeddingBackfill>();

        services.AddSingleton<IReviewQueue, ValkeyReviewQueue>();
        services.AddSingleton<IReviewStatusStore, ValkeyReviewStatusStore>();
        services.AddSingleton<ReviewProgress>();
        services.AddHostedService<ReviewWorker>();

        services.AddSingleton<IManualQueue, ValkeyManualQueue>();
        services.AddScoped<ManualProgress>();
        services.AddHostedService<ManualParseWorker>();
        
        services.AddScoped<IExpirationSustainerTool, ExpirationSustainerTool>();
        
        
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IManualRepository, ManualRepository>();
        services.AddScoped<IPinRepository, PinRepository>();
        services.AddScoped<IAssignmentRepository, AssignmentRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<ILetterTemplateRepository, LetterTemplateRepository>();
        services.AddScoped<IOrganizationProfileRepository, OrganizationProfileRepository>();
        services.AddScoped<IDatasetRepository, DatasetRepository>();
        services.AddScoped<IDatasetQueryRunner, DatasetQueryRunner>();
        services.AddScoped<ITokenUsageRepository, TokenUsageRepository>();
        services.AddScoped<ITokenMeter, TokenMeter>();
        services.AddSingleton<BalanceProbe>();

        services.AddGigaChatInfrastructure(configuration);

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddHostedService<StaffAccountSeeder>();

        services.AddScoped<IJwtProvider, JwtProvider>();
        services.AddScoped<ITicketProvider, TicketProvider>();
        services.AddScoped<IDocxAnonymizer, DocxAnonymizer>();
        services.AddSingleton<IDocxTextExtractor, DocxTextExtractor>();
        services.AddSingleton<IDocxWriter, DocxWriter>();
        services.AddSingleton<IPdfTextExtractor, PdfTextExtractor>();
        services.AddSingleton<IDocxTemplateFiller, DocxTemplateFiller>();
        services.AddSingleton<IKnowledgeDocumentReader, KnowledgeDocumentReader>();

        return services;
    }
}