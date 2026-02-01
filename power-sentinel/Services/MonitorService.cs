using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PowerSentinel.Data;
using PowerSentinel.Models;

namespace PowerSentinel.Services;

public class MonitorOptions
{
    public int IntervalSeconds { get; set; } = 30;
    public int HeartbeatTimeoutSeconds { get; set; } = 120;
}

public class MonitorService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly MonitorOptions _opts;
    private readonly ITelegramBotService? _telegram;

    public MonitorService(IServiceProvider services, IOptions<MonitorOptions> opts, IServiceProvider provider)
    {
        _services = services;
        _opts = opts.Value;
        // resolve telegram service if registered
        _telegram = provider.GetService(typeof(ITelegramBotService)) as ITelegramBotService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var lastEvent = await db.Events.OrderByDescending(e => e.StartAt).FirstOrDefaultAsync(stoppingToken);

                var now = DateTime.Now;

                // consider power alive if any registered device has a recent heartbeat
                var recentThreshold = now.AddSeconds(-_opts.HeartbeatTimeoutSeconds);
                var isAlive = await db.Devices.AnyAsync(d => d.Heartbeat.HasValue && d.Heartbeat >= recentThreshold, stoppingToken);

                if (lastEvent == null)
                {
                    // initialize: only create an initial event if we have any device records
                    var lastSeen = await db.Devices.OrderByDescending(d => d.Heartbeat).FirstOrDefaultAsync(stoppingToken);
                    if (lastSeen != null)
                    {
                        var initial = new Event { IsPowerOn = isAlive, StartAt = now, DeviceId = lastSeen.Id };
                        db.Events.Add(initial);
                        await db.SaveChangesAsync(stoppingToken);
                    }
                }
                else
                {
                    if (isAlive && lastEvent.IsPowerOn == false)
                    {
                        // power resumed
                        lastEvent.EndAt = now;
                        var resumedDevice = await db.Devices.Where(d => d.Heartbeat.HasValue && d.Heartbeat >= recentThreshold)
                            .OrderByDescending(d => d.Heartbeat).FirstOrDefaultAsync(stoppingToken);
                        var onEvent = new Event { IsPowerOn = true, StartAt = now, DeviceId = resumedDevice?.Id };
                        db.Events.Add(onEvent);
                        await db.SaveChangesAsync(stoppingToken);

                        // notify Telegram subscribers
                        try
                        {
                            var dur = (lastEvent.EndAt - lastEvent.StartAt);
                            await _telegram?.SendPowerNotificationAsync(true, dur, resumedDevice?.Id, stoppingToken);
                        }
                        catch { }
                    }
                    else if (!isAlive && lastEvent.IsPowerOn == true)
                    {
                        // power lost
                        lastEvent.EndAt = now;
                        var lastSeen = await db.Devices.OrderByDescending(d => d.Heartbeat).FirstOrDefaultAsync(stoppingToken);
                        var offEvent = new Event { IsPowerOn = false, StartAt = now, DeviceId = lastSeen?.Id };
                        db.Events.Add(offEvent);
                        await db.SaveChangesAsync(stoppingToken);

                        // notify Telegram subscribers
                        try
                        {
                            var dur = (lastEvent.EndAt - lastEvent.StartAt);
                            await _telegram?.SendPowerNotificationAsync(false, dur, lastSeen?.Id, stoppingToken);
                        }
                        catch { }
                    }
                }
            }
            catch
            {
                // ignore errors for now
            }

            await Task.Delay(TimeSpan.FromSeconds(_opts.IntervalSeconds), stoppingToken);
        }
    }
}
