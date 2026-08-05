using ChatNode.Application.Services;
using ChatNode.Application.Services.Abstractons;

namespace ChatNode.Application;

public static class ApplicationInjector
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IManualService, ManualService>();
        return services;
    }
}