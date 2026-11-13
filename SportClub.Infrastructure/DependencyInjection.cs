using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportClub.Core.Interfaces;
using SportClub.Infrastructure.Data;
using SportClub.Infrastructure.Repositories;

namespace SportClub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITrainerRepository, TrainerRepository>();
        services.AddScoped<ITrainingTypeRepository, TrainingTypeRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IWaitlistRepository, WaitlistRepository>();
        services.AddScoped<IMonthlyPassRepository, MonthlyPassRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IPassPlanRepository, PassPlanRepository>();
        return services;
    }
}
