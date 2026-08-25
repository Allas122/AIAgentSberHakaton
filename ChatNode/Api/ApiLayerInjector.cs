using ChatNode.Api.Configuration;
using ChatNode.Api.Rest.Validators;
using ChatNode.Api.WebSockets.Hubs;
using ChatNode.Infrastructure.Manuals;
using ChatNode.Infrastructure.Review;
using ChatNode.Infrastructure.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
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

        services.Configure<KestrelServerOptions>(
            options => options.Limits.MaxRequestBodySize = UploadLimits.MaxRequestBytes);

        services.Configure<FormOptions>(
            options => options.MultipartBodyLengthLimit = UploadLimits.MaxRequestBytes);

        services.AddSingleton<IReviewNotifier, HubReviewNotifier>();
        services.AddSingleton<IManualNotifier, HubManualNotifier>();

        services.AddHttpContextAccessor();

        services.AddProblemDetails();
        services.AddExceptionHandler<DomainExceptionHandler>();

        services.AddValidatorsFromAssemblyContaining<UploadFileRequestValidator>();
        services.AddFluentValidationAutoValidation();
        services.AddAuthConfiguration(configuration);
        services.AddScalarConfiguration();

        return services;
    }

    public static WebApplication MapApiLayer(this WebApplication app)
    {
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment()) app.UseHttpsRedirection();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHub<ChatHub>("/chat-hub");
        app.MapControllers();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        return app;
    }
}