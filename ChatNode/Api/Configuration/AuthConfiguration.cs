using System.Text;
using ChatNode.Infrastructure.Auth;
using ChatNode.Infrastructure.Configuration.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Domain.ValueTypes;

namespace ChatNode.Api.Configuration;

public static class AuthConfiguration
{
    public static IServiceCollection AddAuthConfiguration(this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>()
                         ?? throw new InvalidOperationException(
                             "Секция конфигурации 'Jwt' не задана — нечем подписывать токены.");

        var key = Encoding.UTF8.GetBytes(jwtOptions.SecretKey ?? string.Empty);

        if (key.Length < 32)
        {
            throw new InvalidOperationException(
                $"Jwt:SecretKey должен быть минимум 32 байта, сейчас {key.Length}. " +
                "Задайте JWT_SECRET_KEY длиннее.");
        }

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ClockSkew = TimeSpan.Zero
                };
            }).AddScheme<AuthenticationSchemeOptions,TicketAuthHandler>("WebSocketScheme", null);

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthPolicies.Staff, policy =>
                policy.RequireRole(
                    nameof(UserRole.Rector),
                    nameof(UserRole.Coordinator)));
        });

        return services;
    }
}

public static class AuthPolicies
{
    public const string Staff = "Staff";
}