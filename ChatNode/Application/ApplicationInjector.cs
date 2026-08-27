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
        services.AddScoped<ILetterService, LetterService>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ILetterTemplateService, LetterTemplateService>();
        services.AddScoped<IOrganizationProfileService, OrganizationProfileService>();
        services.AddScoped<IUsageService, UsageService>();
        return services;
    }
}