using ChatNode.Api.Configuration;
using ChatNode.Api.Rest.Validators;
using ChatNode.Api.WebSockets.Hubs;
using FluentValidation;
using Scalar.AspNetCore;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;

namespace ChatNode.Api;

public static class ApiLayerInjector
{
    public static IServiceCollection AddApiLayer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddSignalR();
        services.AddEndpointsApiExplorer();
        services.AddOpenApi();

        services.AddValidatorsFromAssemblyContaining<UploadFileRequestValidator>();
        services.AddFluentValidationAutoValidation();
        services.AddAuthConfiguration(configuration);
        services.AddScalarConfiguration();

        return services;
    }

    public static WebApplication MapApiLayer(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHub<ChatHub>("/chat-hub");
        app.MapOpenApi();
        app.MapScalarApiReference();
        app.UseHttpsRedirection();
        app.MapControllers();

        return app;
    }
}