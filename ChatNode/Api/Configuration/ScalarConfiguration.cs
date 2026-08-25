using Microsoft.OpenApi;

namespace ChatNode.Api.Configuration;

public static class ScalarConfiguration
{
    public static IServiceCollection AddScalarConfiguration(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                };

                document.Security ??= [];
                document.Security.Add(new OpenApiSecurityRequirement
                {
                    { new OpenApiSecuritySchemeReference("Bearer", document), new List<string>() }
                });

                return Task.CompletedTask;
            });
        });

        return services;
    }
}
