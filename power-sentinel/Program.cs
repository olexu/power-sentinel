using Microsoft.EntityFrameworkCore;
using PowerSentinel.Data;
using PowerSentinel.Services;

var builder = WebApplication.CreateBuilder(args);

// Ensure the app listens on a stable default port (5001) when ASPNETCORE_URLS is not set.
var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://*:5001";
builder.WebHost.UseUrls(urls);

builder.Services.AddRazorPages();
builder.Services.AddControllers();

var connectionString = builder.Configuration["DatabaseConnectionString"] ?? "Data Source=power-sentinel.db";
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

builder.Services.Configure<MonitorOptions>(opts =>
{
    opts.IntervalSeconds = builder.Configuration.GetValue<int?>("MonitorIntervalSeconds") ?? opts.IntervalSeconds;
    opts.HeartbeatTimeoutSeconds = builder.Configuration.GetValue<int?>("MonitorTimeoutSeconds") ?? opts.HeartbeatTimeoutSeconds;
});

builder.Services.Configure<HeartbeatOptions>(opts =>
{
    var heartbeatToken = builder.Configuration["HeartbeatToken"];
    if (!string.IsNullOrEmpty(heartbeatToken)) opts.HeartbeatToken = heartbeatToken;
});

builder.Services.Configure<TelegramOptions>(opts =>
{
    var botToken = builder.Configuration["TelegramBotToken"];
    if (!string.IsNullOrWhiteSpace(botToken)) opts.BotToken = botToken;
    var publicUrl = builder.Configuration["PublicUrl"];
    if (!string.IsNullOrEmpty(publicUrl)) opts.PublicUrl = publicUrl;
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
