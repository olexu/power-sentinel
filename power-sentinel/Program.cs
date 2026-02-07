using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using PowerSentinel.Data;
using PowerSentinel.Services;

var builder = WebApplication.CreateBuilder(args);

// Ensure the app listens on a stable default port (5001) when ASPNETCORE_URLS is not set.
var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://*:5001";
builder.WebHost.UseUrls(urls);

builder.Services.AddRazorPages();
builder.Services.AddControllers();

// Cookie-based authentication for Admin area
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Admin/Login";
        options.LogoutPath = "/Admin/Logout";
        options.Cookie.Name = "PowerSentinelAuth";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin").AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme));
});

var connectionString = builder.Configuration["DatabaseConnectionString"] ?? "Data Source=power-sentinel.db";
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

builder.Services.AddSingleton<TelegramBotService>();
builder.Services.AddSingleton<ITelegramBotService>(sp => sp.GetRequiredService<TelegramBotService>());

builder.Services.AddHostedService(sp => sp.GetRequiredService<TelegramBotService>());
builder.Services.AddHostedService<MonitorService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        DbUpMigration.ApplyMigrations(builder.Configuration["DatabaseConnectionString"] ?? "Data Source=power-sentinel.db");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DbUp migration failed: {ex.Message}");
        throw;
    }
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();

app.Run();
