using ChatNode.Infrastructure.AI.Configuration;
using ChatNode.Infrastructure.Auth;
using ChatNode.Infrastructure.Auth.Abstractions;
using ChatNode.Infrastructure.Configuration;
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

        services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(valkeyConnectionString));
        services.AddScoped<IDatabase>(sp => sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());
        
        services.AddMyConfiguration(configuration);
        
        services.AddStorage();

        services.AddHostedService<ValkeyIndexInitializer>();
        
        services.AddScoped<IExpirationSustainerTool, ExpirationSustainerTool>();
        
        
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IManualRepository, ManualRepository>();
        services.AddScoped<IPinRepository, PinRepository>();

        services.AddGigaChatInfrastructure(configuration);

        services.AddScoped<IJwtProvider, JwtProvider>();
        services.AddScoped<ITicketProvider, TicketProvider>();
        services.AddScoped<IDocxAnonymizer, DocxAnonymizer>();
        services.AddSingleton<IDocxTextExtractor, DocxTextExtractor>();
        
        return services;
    }
}