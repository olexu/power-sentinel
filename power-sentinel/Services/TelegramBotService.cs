using Microsoft.Extensions.Options;
using PowerSentinel.Data;
using Microsoft.EntityFrameworkCore;
using PowerSentinel.Models;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using System.Net;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace PowerSentinel.Services;

public class TelegramOptions
{
    public string? BotToken { get; set; }
    // optional public application URL used to build links to the web UI
    public string? PublicUrl { get; set; }
}

public interface ITelegramBotService
{
    Task SendPowerNotificationAsync(bool isOn, TimeSpan? previousDuration, string? deviceId = null, CancellationToken ct = default);
}

public class TelegramBotService : BackgroundService, ITelegramBotService
{
    private readonly TelegramOptions _opts;
    private readonly IServiceProvider _services;
    private TelegramBotClient? _client;

    public TelegramBotService(IOptions<TelegramOptions> opts, IServiceProvider services)
    {
        _opts = opts.Value;
        _services = services;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_opts.BotToken))
        {
            _client = new TelegramBotClient(_opts.BotToken!);
        }

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_client == null) return;

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>() // receive all
        };

        _client.StartReceiving(HandleUpdateAsync, HandlePollingErrorAsync, receiverOptions, stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException) { }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        return base.StopAsync(cancellationToken);
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
    {
        // swallow for now
        return Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery?.Data != null)
        {
            var cq = update.CallbackQuery;
            var data = cq.Data;
            var cqChatId = cq.Message?.Chat.Id ?? cq.From.Id;

            using var scopeCb = _services.CreateScope();
            var dbCb = scopeCb.ServiceProvider.GetRequiredService<AppDbContext>();

            var existingSub = await dbCb.Subscribers.FirstOrDefaultAsync(s => s.ChatId == cqChatId, ct);
            if (existingSub == null)
            {
                existingSub = new Subscriber { ChatId = cqChatId, IsActive = true };
                dbCb.Subscribers.Add(existingSub);
            }

                if (data == "subscribe_all")
            {
                existingSub.DeviceId = null;
                existingSub.IsActive = true;
                await dbCb.SaveChangesAsync(ct);
                await botClient.AnswerCallbackQuery(cq.Id, "Subscribed: all", cancellationToken: ct);
                if (cq.Message != null)
                    await botClient.EditMessageText(cq.Message.Chat.Id, cq.Message.MessageId, "Subscribed: all", cancellationToken: ct);
                return;
            }

            if (data.StartsWith("subscribe:"))
            {
                var deviceId = data.Substring("subscribe:".Length);
                var dev = await dbCb.Devices.FindAsync(new object[] { deviceId }, ct);
                if (dev == null)
                {
                    await botClient.AnswerCallbackQuery(cq.Id, "Device not found", cancellationToken: ct);
                    return;
                }

                existingSub.DeviceId = dev.Id;
                existingSub.IsActive = true;
                await dbCb.SaveChangesAsync(ct);
                string confirmText = $"Subscribed: {WebUtility.HtmlEncode(dev.Description ?? dev.Id)}.";
                if (!string.IsNullOrWhiteSpace(_opts.PublicUrl))
                {
                    var url = _opts.PublicUrl!.TrimEnd('/') + "/?deviceId=" + Uri.EscapeDataString(dev.Id);
                    confirmText += "\n🔗 " + $"<a href=\"{WebUtility.HtmlEncode(url)}\">Outage Statistics</a>";
                }
                await botClient.AnswerCallbackQuery(cq.Id, $"Subscribed: {dev.Description ?? dev.Id}", cancellationToken: ct);
                if (cq.Message != null)
                    await botClient.EditMessageText(cq.Message.Chat.Id, cq.Message.MessageId, confirmText, parseMode: ParseMode.Html, cancellationToken: ct);
                return;
            }

            return;
        }

        if (update.Type != UpdateType.Message || update.Message?.Text == null) return;

        var msg = update.Message;
        var chatId = msg.Chat.Id;
        var text = msg.Text.Trim();

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (text.StartsWith("/start"))
        {
            var existing = await db.Subscribers.FirstOrDefaultAsync(s => s.ChatId == chatId, ct);
            if (existing == null)
            {
                db.Subscribers.Add(new Subscriber { ChatId = chatId, IsActive = true });
                await db.SaveChangesAsync(ct);
            }
            else if (!existing.IsActive)
            {
                existing.IsActive = true;
                await db.SaveChangesAsync(ct);
            }

            await botClient.SendMessage(chatId, "Subscribed. /stop — unsubscribe · /devices — list · /status", cancellationToken: ct);
        }
        else if (text.StartsWith("/stop"))
        {
            var existing = await db.Subscribers.FirstOrDefaultAsync(s => s.ChatId == chatId, ct);
            if (existing != null)
            {
                existing.IsActive = false;
                await db.SaveChangesAsync(ct);
            }

            await botClient.SendMessage(chatId, "You are unsubscribed.", cancellationToken: ct);
        }
        else if (text.StartsWith("/devices"))
        {
            var devices = await db.Devices.OrderBy(d => d.Id).ToListAsync(ct);
                if (devices.Count == 0)
                {
                    await botClient.SendMessage(chatId, "No devices.", cancellationToken: ct);
                }
                else
                {
                // build inline keyboard with one button per device
                var buttons = devices.Select(d => new[] { InlineKeyboardButton.WithCallbackData($"{d.Description ?? d.Id}", $"subscribe:{d.Id}") }).ToArray();
                // add a button to subscribe to all devices
                var allButton = new[] { InlineKeyboardButton.WithCallbackData("All devices", "subscribe_all") };
                var keyboardRows = new List<InlineKeyboardButton[]> { allButton };
                keyboardRows.AddRange(buttons);
                var keyboard = new InlineKeyboardMarkup(keyboardRows);

                await botClient.SendMessage(chatId, "Device list — select to subscribe.", replyMarkup: keyboard, cancellationToken: ct);
            }
        }
        else if (text.StartsWith("/subscribe"))
        {
            var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string? deviceId = null;
            if (parts.Length > 1)
                deviceId = parts[1].Trim();

            if (deviceId != null)
            {
                // try exact id
                var dev = await db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct);
                if (dev == null)
                {
                    // try case-insensitive id
                    var idLower = deviceId.ToLowerInvariant();
                    dev = await db.Devices.FirstOrDefaultAsync(d => d.Id.ToLower() == idLower, ct);
                }

                if (dev == null)
                {
                    // try by description contains
                    var descLower = deviceId.ToLowerInvariant();
                    dev = await db.Devices.FirstOrDefaultAsync(d => d.Description != null && d.Description.ToLower().Contains(descLower), ct);
                }

                if (dev == null)
                {
                    await botClient.SendMessage(chatId, "Device not found.", cancellationToken: ct);
                    return;
                }

                // normalize to canonical id from DB
                deviceId = dev.Id;
            }

            var existing = await db.Subscribers.FirstOrDefaultAsync(s => s.ChatId == chatId, ct);
            if (existing == null)
            {
                db.Subscribers.Add(new Subscriber { ChatId = chatId, IsActive = true, DeviceId = deviceId });
                await db.SaveChangesAsync(ct);
            }
            else
            {
                existing.IsActive = true;
                existing.DeviceId = deviceId;
                await db.SaveChangesAsync(ct);
            }

            string response;
            if (deviceId == null)
            {
                response = "Subscribed: all";
                await botClient.SendMessage(chatId, response, cancellationToken: ct);
            }
            else
            {
                response = $"Subscribed: {WebUtility.HtmlEncode(deviceId)}.";
                if (!string.IsNullOrWhiteSpace(_opts.PublicUrl))
                {
                    var url = _opts.PublicUrl!.TrimEnd('/') + "/?deviceId=" + Uri.EscapeDataString(deviceId);
                    response += "\n🔗 " + $"<a href=\"{WebUtility.HtmlEncode(url)}\">Statistics</a>";
                    await botClient.SendMessage(chatId, response, parseMode: ParseMode.Html, cancellationToken: ct);
                }
                else
                {
                    await botClient.SendMessage(chatId, response, cancellationToken: ct);
                }
            }
        }
        else if (text.StartsWith("/status"))
        {
            var existing = await db.Subscribers.FirstOrDefaultAsync(s => s.ChatId == chatId, ct);
            if (existing == null || !existing.IsActive)
            {
                await botClient.SendMessage(chatId, "You are not subscribed.", cancellationToken: ct);
                return;
            }

            if (string.IsNullOrWhiteSpace(existing.DeviceId))
            {
                await botClient.SendMessage(chatId, "Current subscription: all", cancellationToken: ct);
            }
            else
            {
                var dev = await db.Devices.FindAsync(new object[] { existing.DeviceId }, ct);
                var desc = dev != null ? (dev.Description ?? dev.Id) : existing.DeviceId;
                await botClient.SendMessage(chatId, $"Current subscription: {desc} (id:{existing.DeviceId})", cancellationToken: ct);
            }
        }
        else
        {
            await botClient.SendMessage(chatId, "Available commands: /start, /stop", cancellationToken: ct);
        }
    }

    public async Task SendPowerNotificationAsync(bool isOn, TimeSpan? previousDuration, string? deviceId = null, CancellationToken ct = default)
    {
        if (_client == null) return;

        var dot = isOn ? "🟢" : "🔴";
        var durText = previousDuration.HasValue ? FormatDuration(previousDuration.Value) : "—";
        string text;

        // Include device name if provided
        string deviceText = string.Empty;
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            using var scopeDevice = _services.CreateScope();
            var dbDevice = scopeDevice.ServiceProvider.GetRequiredService<AppDbContext>();
            var dev = await dbDevice.Devices.FindAsync(new object[] { deviceId }, ct);
            if (dev != null)
                deviceText = dev.Description ?? dev.Id;
            else
                deviceText = deviceId;
        }

        if (isOn)
            text = $"{dot} {deviceText} ON.\n⏱️ Outage: {durText}.";
        else
            text = $"{dot} {deviceText} OFF.\n⏱️ Uptime: {durText}.";

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // send to subscribers who are active and either subscribed to all (DeviceId == null)
        // or subscribed specifically to this device
        var subs = await db.Subscribers.Where(s => s.IsActive && (s.DeviceId == null || s.DeviceId == deviceId)).ToListAsync(ct);

        foreach (var s in subs)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_opts.PublicUrl))
                {
                    await _client.SendMessage(s.ChatId, text, parseMode: ParseMode.Html, cancellationToken: ct);
                }
                else
                {
                    await _client.SendMessage(s.ChatId, text, cancellationToken: ct);
                }
            }
            catch
            {
                // ignore per-subscriber errors
            }
        }
    }

    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
        return string.Format("{0:D2}:{1:D2}", ts.Minutes, ts.Seconds);
    }
}
