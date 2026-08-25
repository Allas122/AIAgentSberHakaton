using Microsoft.EntityFrameworkCore;

namespace ChatNode.Infrastructure.Database;

public static class DatabaseInjector
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Строка подключения ConnectionStrings__Postgres не задана. " +
                "Постоянные данные (поручения, аккаунты, методички) хранятся в PostgreSQL, без неё сервис не работает.");
        }

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddHostedService<DatabaseMigrator>();

        return services;
    }
}
