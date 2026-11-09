using AquaTrack.Application.Common;
using AquaTrack.Domain.Entities;
using AquaTrack.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AquaTrack.Infrastructure.Persistence;

/// <summary>
/// Runs at startup: applies migrations, creates the 4 roles, the Super Admin login and the default plans.
/// Safe to run every time (only adds what is missing).
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var config = services.GetRequiredService<IConfiguration>();

        await db.Database.MigrateAsync();

        // Roles
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // Super Admin (credentials come from configuration / user-secrets, never from code)
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var adminPhone = config["SuperAdmin:Phone"];
        var adminPassword = config["SuperAdmin:Password"];

        if (!string.IsNullOrWhiteSpace(adminPhone) && !string.IsNullOrWhiteSpace(adminPassword)
            && await userManager.FindByNameAsync(adminPhone) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminPhone,
                PhoneNumber = adminPhone,
                Email = config["SuperAdmin:Email"],
                FullName = config["SuperAdmin:FullName"] ?? "Super Admin",
                PlantId = null
            };

            var result = await userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, Roles.SuperAdmin);
                logger.LogInformation("Super Admin created: {Phone}", adminPhone);
            }
            else
            {
                logger.LogError("Super Admin not created: {Errors}",
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }

        // Default plans (SRS 8, pricing draft)
        if (!await db.Plans.IgnoreQueryFilters().AnyAsync())
        {
            db.Plans.AddRange(
                new Plan { Name = "Basic", MaxCustomers = 200, MaxDeliveryBoys = 2, MonthlyPrice = 1000, IncludesWhatsAppApi = false },
                new Plan { Name = "Pro", MaxCustomers = null, MaxDeliveryBoys = null, MonthlyPrice = 2000, IncludesWhatsAppApi = true });

            await db.SaveChangesAsync();
        }
    }
}
