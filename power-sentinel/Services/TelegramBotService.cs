using PowerSentinel.Data;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using PowerSentinel.Helpers;
using System.Net;

namespace PowerSentinel.Services;

public interface ITelegramBotService
{
    Task SendPowerNotificationAsync(string deviceId, string deviceDescription, bool isOn, TimeSpan? lastEventDuration, CancellationToken ct = default);
}

public class TelegramBotService : ITelegramBotService, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _services;
    private readonly ILogger<TelegramBotService>? _logger;
    private TelegramBotClient? _client;

    public TelegramBotService(IConfiguration configuration, IServiceProvider services, ILogger<TelegramBotService> logger)
    {
        _configuration = configuration;
        _services = services;
        _logger = logger;

        var telegramBotToken = _configuration["TelegramBotToken"];
        if (!string.IsNullOrWhiteSpace(telegramBotToken))
        {
            _client = new TelegramBotClient(telegramBotToken);
            _logger?.LogInformation("Telegram bot client created.");
        }
        else
        {
            _logger?.LogWarning("Telegram bot token not configured; Telegram bot disabled.");
        }
    }

    public void Dispose()
    {
        if (_client is IDisposable d)
            d.Dispose();
    }

    public async Task SendPowerNotificationAsync(string deviceId, string deviceDescription, bool isOn, TimeSpan? lastEventDuration, CancellationToken ct = default)
    {
        if (_client == null) return;

        var publicUrl = _configuration["PublicUrl"];

        string text;

        var device = deviceDescription;

        if (!string.IsNullOrWhiteSpace(publicUrl))
        {
            var url = publicUrl.TrimEnd('/') + "/Statistic?deviceId=" + Uri.EscapeDataString(deviceId);
            device = $"<a href=\"{WebUtility.HtmlEncode(url)}\">{WebUtility.HtmlEncode(deviceDescription)}</a>";
        }

        if (isOn)
            text = $"🟢 {device} is ON.\n🕒 Downtime: {lastEventDuration.ToDisplayString()}";
        else
            text = $"🔴 {device} is OFF.\n🕒 Uptime: {lastEventDuration.ToDisplayString()}";

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var subscribers = await db.Subscribers.Where(s => s.IsActive && (s.DeviceId == null || s.DeviceId == deviceId)).ToListAsync(ct);

        foreach (var s in subscribers)
        {
            try
            {
                await _client.SendMessage(s.ChatId, text, Telegram.Bot.Types.Enums.ParseMode.Html, cancellationToken: ct);
            }
            catch
            {
                _logger?.LogWarning("Failed to send Telegram message to chat {ChatId}", s.ChatId);
            }
        }
    }
}
