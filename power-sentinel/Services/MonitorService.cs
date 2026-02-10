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
                var heartbeatCheckTime = now.AddSeconds(-_configuration.GetValue("Monitor:HeartbeatAliveSeconds", 15));

                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var devices = await db.Devices.ToListAsync(stoppingToken);
                foreach (var device in devices)
                {
                    var isPowerOn = device.Heartbeat.HasValue && device.Heartbeat >= heartbeatCheckTime;

                    var latestDeviceEvent = await db.Events
                        .Where(e => e.DeviceId == device.Id)
                        .OrderByDescending(e => e.Date)
                        .FirstOrDefaultAsync(stoppingToken);

                    if (latestDeviceEvent != null && latestDeviceEvent.IsPowerOn == isPowerOn)
                    {
                        continue;
                    }

                    var newEvent = new Event { DeviceId = device.Id, IsPowerOn = isPowerOn, Date = now };
                    db.Events.Add(newEvent);
                    await db.SaveChangesAsync(stoppingToken);

                    if (latestDeviceEvent != null)
                    {
                        try
                        {
                            var duration = now - latestDeviceEvent.Date;
                            var sendTask = _telegram?.SendPowerNotificationAsync(device.Id, device.Description ?? device.Id, isPowerOn, duration, stoppingToken);
                            if (sendTask != null) await sendTask;
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Failed to send resume notification for device {DeviceId}", device.Id);
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
