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
using PowerSentinel.Helpers;

namespace PowerSentinel.Services;

public interface ITelegramBotService
{
    Task SendPowerNotificationAsync(bool isOn, string deviceId, string description, TimeSpan? previousDuration, CancellationToken ct = default);
}

public class TelegramBotService : BackgroundService, ITelegramBotService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _services;
    private TelegramBotClient? _client;

    private readonly ILogger<TelegramBotService>? _logger;

    public TelegramBotService(IConfiguration configuration, IServiceProvider services, ILogger<TelegramBotService> logger)
    {
        _configuration = configuration;
        _services = services;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        var telegramBotToken = _configuration["TelegramBotToken"];
        if (!string.IsNullOrWhiteSpace(telegramBotToken))
        {
            _client = new TelegramBotClient(telegramBotToken);
            _logger?.LogInformation("Telegram bot client created.");
        }
        else
        {
            _logger?.LogWarning("Telegram bot token not configured; Telegram polling disabled.");
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
        _logger?.LogInformation("Telegram polling started.");

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
        // Log polling errors for diagnosis
        _logger?.LogError(exception, "Telegram polling error");
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
                if (!string.IsNullOrWhiteSpace(_configuration["PublicUrl"]))
                {
                    var url = _configuration["PublicUrl"]!.TrimEnd('/') + "/?deviceId=" + Uri.EscapeDataString(dev.Id);
                    confirmText += "\n🔗 " + $"<a href=\"{WebUtility.HtmlEncode(url)}\">Device Statistic</a>";
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
                if (!string.IsNullOrWhiteSpace(_configuration["PublicUrl"]))
                {
                    var url = _configuration["PublicUrl"]!.TrimEnd('/') + "/?deviceId=" + Uri.EscapeDataString(deviceId);
                    response += "\n🔗 " + $"<a href=\"{WebUtility.HtmlEncode(url)}\">Device Statistic</a>";
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

    public async Task SendPowerNotificationAsync(bool isOn, string deviceId, string deviceDescription, TimeSpan? prevEventDuration, CancellationToken ct = default)
    {
        if (_client == null) return;

        string text;

        if (isOn)
            text = $"🟢 {deviceDescription} is ON.\n⏱️ Downtime: {prevEventDuration.ToDisplayString()}";
        else
            text = $"🔴 {deviceDescription} is OFF.\n⏱️ Uptime: {prevEventDuration.ToDisplayString()}";

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subs = await db.Subscribers.Where(s => s.IsActive && (s.DeviceId == null || s.DeviceId == deviceId)).ToListAsync(ct);

        foreach (var s in subs)
        {
            try
            {
                await _client.SendMessage(s.ChatId, text, cancellationToken: ct);
            }
            catch
            {
                _logger?.LogWarning("Failed to send Telegram message to chat {ChatId}", s.ChatId);
            }
        }
    }
}
