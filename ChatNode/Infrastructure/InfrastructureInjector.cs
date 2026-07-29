using Amazon.S3;
using ChatNode.Infrastructure.AI.Configuration;
using ChatNode.Infrastructure.Auth;
using ChatNode.Infrastructure.Auth.Abstractions;
using ChatNode.Infrastructure.Configuration;
using ChatNode.Infrastructure;
using ChatNode.Infrastructure.Configuration.Options;
using ChatNode.Infrastructure.Tools;
using ChatNode.Infrastructure.Tools.Abstractions;
using ChatNode.Infrastructure.Services;
using Domain.Repositories;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ChatNode.Infrastructure;

public static class InfrastructureInjector
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var valkeyConnectionString = configuration.GetConnectionString("Valkey");

        services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(valkeyConnectionString));
        services.AddScoped<IDatabase>(sp => sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());
        
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var s3Opt = sp.GetRequiredService<IOptions<S3Options>>().Value;
            var config = new AmazonS3Config
            {
                ServiceURL = s3Opt.ServiceUrl,
                ForcePathStyle = s3Opt.ForcePathStyle,
                UseHttp = s3Opt.UseHttp
            };
            return new AmazonS3Client(s3Opt.AccessKey, s3Opt.SecretKey, config);
        });

        services.AddHostedService<ValkeyIndexInitializer>();

        services.AddMyConfiguration(configuration);

        services.AddScoped<IExpirationSustainerTool, ExpirationSustainerTool>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IManualRepository, ManualRepository>();

        services.AddGigaChatInfrastructure(configuration);

        services.AddScoped<IJwtProvider, JwtProvider>();
        services.AddScoped<ITicketProvider, TicketProvider>();

        return services;
    }
}