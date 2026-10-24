using Advisor.Application.Obis;
using Advisor.Infrastructure.Auth;
using Advisor.Infrastructure.Obis;
using Advisor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Advisor.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default") ?? "Data Source=advisor.db";
        services.AddDbContext<AdvisorDbContext>(options => options.UseSqlite(connectionString));

        services.AddMemoryCache();
        services.Configure<ObisOptions>(configuration.GetSection(ObisOptions.SectionName));
        services.AddHttpClient<IObisClient, HttpObisClient>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<AuthService>();

        return services;
    }
}
