using System.Globalization;
using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Infrastructure;
using AquaTrack.Infrastructure.Persistence;
using AquaTrack.Web;
using AquaTrack.Web.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;

var builder = WebApplication.CreateBuilder(args);

// ---- Services ----
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<WhatsAppMessages>();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);   // delivery boys stay logged in on their phone
    options.SlidingExpiration = true;
    options.Cookie.Name = "AquaTrack.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

    // Without "Keep me signed in": the login lasts only while the browser is open AND the app keeps running.
    // A browser close ends the session cookie; an app restart changes AppSession.BootId and the old login is refused.
    options.Events.OnSigningIn = ctx =>
    {
        if (ctx.Properties.IsPersistent != true && ctx.Principal?.Identity is ClaimsIdentity id)
            id.AddClaim(new Claim(AppSession.BootClaim, AppSession.BootId));
        return Task.CompletedTask;
    };

    var identityCheck = options.Events.OnValidatePrincipal; // security-stamp validation from Identity
    options.Events.OnValidatePrincipal = async ctx =>
    {
        if (ctx.Properties.IsPersistent != true &&
            ctx.Principal?.FindFirst(AppSession.BootClaim)?.Value != AppSession.BootId)
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return;
        }
        await identityCheck(ctx);
    };
});

// Identity re-checks logins every 30 minutes and rebuilds the user's claims; carry the boot id over.
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.OnRefreshingPrincipal = ctx =>
{
    var boot = ctx.CurrentPrincipal?.FindFirst(AppSession.BootClaim);
    if (boot is not null && ctx.NewPrincipal?.Identity is ClaimsIdentity id)
        id.AddClaim(new Claim(boot.Type, boot.Value));
    return Task.CompletedTask;
});

// SRS SYS-02: English + Urdu (Urdu is right-to-left)
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = new[] { new CultureInfo(AppCultures.English), new CultureInfo(AppCultures.Urdu) };
    options.DefaultRequestCulture = new RequestCulture(AppCultures.English);
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;
    options.RequestCultureProviders = new List<IRequestCultureProvider> { new CookieRequestCultureProvider() };
});

// Every page needs login unless marked [AllowAnonymous]
var requireLogin = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

builder.Services
    .AddControllersWithViews(options =>
    {
        options.Filters.Add(new AuthorizeFilter(requireLogin));
        // SRS NFR-06: anti-forgery token on every POST
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    })
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource));
    });

var app = builder.Build();

// ---- Database: migrate + seed roles, Super Admin, plans ----
using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

// ---- Pipeline ----
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRequestLocalization();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
