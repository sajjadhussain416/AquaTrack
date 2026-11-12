using AquaTrack.Application.Expenses;
using AquaTrack.Application.Reports;
using AquaTrack.Application.Billing;
using AquaTrack.Application.Operations;
using AquaTrack.Application.Plants;
using AquaTrack.Application.Settings;
using AquaTrack.Infrastructure.Identity;
using AquaTrack.Infrastructure.Persistence;
using AquaTrack.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AquaTrack.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                // Users log in with mobile number as UserName (SRS AUTH-01)
                options.User.RequireUniqueEmail = false;

                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireDigit = true;

                // SRS AUTH-06: lock after 5 failed attempts for 15 minutes
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders()
            .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>();

        services.AddScoped<IPlantService, PlantService>();
        services.AddScoped<IAreaService, AreaService>();
        services.AddScoped<IStaffService, StaffService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IPlantDashboardService, PlantDashboardService>();
        services.AddScoped<IDeliveryService, DeliveryService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPlantSettingsService, PlantSettingsService>();
        services.AddScoped<ICashService, CashService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IExpenseService, ExpenseService>();

        return services;
    }
}
