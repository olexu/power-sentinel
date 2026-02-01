using Microsoft.EntityFrameworkCore;
using System.Globalization;
using PowerSentinel.Data;
using PowerSentinel.Services;

var builder = WebApplication.CreateBuilder(args);

// Ensure the app listens on a stable default port (5001) when ASPNETCORE_URLS is not set.
var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://*:5001";
builder.WebHost.UseUrls(urls);

// Set default culture to Ukrainian so month names and date formats are shown in Ukrainian
var ukCulture = new CultureInfo("uk-UA");
CultureInfo.DefaultThreadCurrentCulture = ukCulture;
CultureInfo.DefaultThreadCurrentUICulture = ukCulture;

builder.Services.AddRazorPages();
builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=power-sentinel.db";
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

builder.Services.Configure<MonitorOptions>(builder.Configuration.GetSection("Monitor"));
builder.Services.Configure<HeartbeatOptions>(builder.Configuration.GetSection("Heartbeat"));
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection("Telegram"));

builder.Services.PostConfigure<TelegramOptions>(opts =>
{
    var publicUrl = builder.Configuration["PublicUrl"];
    if (!string.IsNullOrEmpty(publicUrl))
    {
        opts.PublicUrl = publicUrl;
    }
});
builder.Services.AddSingleton<TelegramBotService>();
builder.Services.AddSingleton<ITelegramBotService>(sp => sp.GetRequiredService<TelegramBotService>());

builder.Services.AddHostedService(sp => sp.GetRequiredService<TelegramBotService>());
builder.Services.AddHostedService<MonitorService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseStaticFiles();
app.MapControllers();
app.MapRazorPages();

app.Run();
