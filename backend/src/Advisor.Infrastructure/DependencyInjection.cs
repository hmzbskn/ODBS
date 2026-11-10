using Advisor.Application.Communication;
using Advisor.Application.Management;
using Advisor.Application.Obis;
using Advisor.Infrastructure.Auth;
using Advisor.Infrastructure.Communication;
using Advisor.Infrastructure.Management;
using Advisor.Infrastructure.Obis;
using Advisor.Infrastructure.Persistence;
using Advisor.Infrastructure.Surveys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Advisor.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default") ?? "Server=localhost;Database=AdvisorDb;Trusted_Connection=True;TrustServerCertificate=True";
        services.AddDbContext<AdvisorDbContext>(options => options.UseSqlServer(connectionString));

        services.AddMemoryCache();
        services.Configure<ObisOptions>(configuration.GetSection(ObisOptions.SectionName));
        services.AddHttpClient<IObisClient, HttpObisClient>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<AuthService>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IStudentDirectory, StudentDirectory>();
        services.AddScoped<ManagementService>();
        services.AddScoped<SurveyService>();
        services.AddScoped<AppointmentService>();
        services.AddScoped<StudentFileService>();
        services.AddScoped<AnnouncementService>();
        services.AddScoped<InboxService>();
        services.AddScoped<PetitionService>();
        services.Configure<UploadOptions>(configuration.GetSection(UploadOptions.SectionName));
        services.TryAddSingleton<IFileStore, LocalFileStore>();

        return services;
    }
}
