using Microsoft.EntityFrameworkCore;
using PowerSentinel.Data;
using PowerSentinel.Models;

namespace PowerSentinel.Services;

public class MonitorService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly ITelegramBotService? _telegram;
    private readonly ILogger<MonitorService>? _logger;

    public MonitorService(IConfiguration configuration, IServiceProvider services, ILogger<MonitorService> logger, ITelegramBotService? telegram)
    {
        _configuration = configuration;
        _services = services;
        _telegram = telegram;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;
                var heartbeatAlive = now.AddSeconds(-_configuration.GetValue("Monitor:HeartbeatAliveSeconds", 15));

                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var devices = await db.Devices.ToListAsync(stoppingToken);
                foreach (var device in devices)
                {
                    var isAlive = device.Heartbeat.HasValue && device.Heartbeat >= heartbeatAlive;

                    var lastEvent = await db.Events
                        .Where(e => e.DeviceId == device.Id)
                        .OrderByDescending(e => e.StartAt)
                        .FirstOrDefaultAsync(stoppingToken);

                    if (lastEvent == null)
                    {
                        var initialEvent = new Event { IsPowerOn = isAlive, StartAt = now, DeviceId = device.Id };
                        db.Events.Add(initialEvent);
                        await db.SaveChangesAsync(stoppingToken);
                        continue;
                    }

                    if (lastEvent.IsPowerOn == isAlive)
                    {
                        continue;
                    }

                    if (isAlive)
                    {
                        lastEvent.EndAt = now;
                        var powerOnEvent = new Event { IsPowerOn = true, StartAt = now, DeviceId = device.Id };
                        db.Events.Add(powerOnEvent);
                        await db.SaveChangesAsync(stoppingToken);
                        try
                        {
                            var duration = now - lastEvent.StartAt;
                            var sendTask = _telegram?.SendPowerNotificationAsync(true, device.Id, device.Description ?? device.Id, duration, stoppingToken);
                            if (sendTask != null) await sendTask;
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Failed to send resume notification for device {DeviceId}", device.Id);
                        }
                    }
                    else
                    {
                        lastEvent.EndAt = now;
                        var powerOffEvent = new Event { IsPowerOn = false, StartAt = now, DeviceId = device.Id };
                        db.Events.Add(powerOffEvent);
                        await db.SaveChangesAsync(stoppingToken);
                        try
                        {
                            var duration = now - lastEvent.StartAt;
                            var sendTask = _telegram?.SendPowerNotificationAsync(false, device.Id, device.Description ?? device.Id, duration, stoppingToken);
                            if (sendTask != null) await sendTask;
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Failed to send outage notification for device {DeviceId}", device.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Monitor service loop error");
            }

            await Task.Delay(TimeSpan.FromSeconds(_configuration.GetValue("Monitor:IntervalSeconds", 15)), stoppingToken);
        }
    }
}
