using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PowerSentinel.Data;

namespace PowerSentinel.Pages;

public class StatisticModel : PageModel
{
    private readonly AppDbContext _db;
    public StatisticModel(AppDbContext db) { _db = db; }

    public string DeviceId { get; private set; } = string.Empty;
    public int Year { get; private set; }
    public int Month { get; private set; }
    public string MonthName { get; private set; } = string.Empty;

    public DeviceInfo DeviceInfo { get; private set; } = new DeviceInfo(string.Empty, string.Empty, null, null);
    public List<DayStat> Days { get; private set; } = new();

    public int OutageCount { get; private set; }
    public double AvgOutageSeconds { get; private set; }
    public double MaxOutageSeconds { get; private set; }
    public double UptimePercent { get; private set; }

    public async Task OnGetAsync(string? deviceId, int? year, int? month)
    {
        // Redirect to index if no deviceId provided
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            Response.Redirect("/");
            return;
        }

        var dateTimeNow = DateTime.Now;

        DeviceId = deviceId;
        Year = year ?? dateTimeNow.Year;
        Month = month ?? dateTimeNow.Month;

        var filterDateTimeStart = new DateTime(Year, Month, 1, 0, 0, 0);
        var monthEnd = filterDateTimeStart.AddMonths(1);
        var filterDateTimeEnd = dateTimeNow < monthEnd ? dateTimeNow : monthEnd;

        MonthName = filterDateTimeStart.ToString("MMMM yyyy");

        var deviceInfo = await _db.Devices
            .Where(d => d.Id == DeviceId)
            .Select(d => new
            {
                device = d,
                lastEvent = _db.Events
                    .Where(e => e.DeviceId == d.Id)
                    .OrderByDescending(e => e.StartAt)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync();
        var lastEvent = deviceInfo?.lastEvent;
        bool? isOn = lastEvent != null && lastEvent.EndAt == null ? lastEvent.IsPowerOn : null;
        TimeSpan? timeSpan = lastEvent != null && lastEvent.EndAt == null ? dateTimeNow - lastEvent.StartAt : null;
        DeviceInfo = new DeviceInfo(deviceInfo?.device.Id ?? string.Empty, deviceInfo?.device.Description ?? deviceInfo?.device.Id ?? string.Empty, isOn, timeSpan);

        var events = await _db.Events.AsQueryable()
            .Where(e =>
                e.DeviceId == DeviceId && (
                    (e.StartAt >= filterDateTimeStart && e.StartAt < filterDateTimeEnd) ||
                    (e.EndAt == null) ||
                    (e.EndAt != null && e.EndAt >= filterDateTimeStart && e.EndAt < filterDateTimeEnd)
                ))
                .OrderBy(e => e.StartAt)
            .ToListAsync();

        double TotalUptimeSeconds = 0;
        double TotalDowntimeSeconds = 0;

        for (int d = 1; d <= DateTime.DaysInMonth(Year, Month); d++)
        {
            var dateTimeStart = new DateTime(Year, Month, d, 0, 0, 0);
            var dateTimeEnd = dateTimeStart.AddDays(1);

            double onSeconds = 0;
            double offSeconds = 0;

            foreach (var ev in events)
            {
                if (ev.StartAt >= dateTimeEnd || ev.EndAt.HasValue && ev.EndAt <= dateTimeStart) continue;

                var evStart = ev.StartAt < dateTimeStart ? dateTimeStart : ev.StartAt;
                var evEnd = ev.EndAt.HasValue ? (ev.EndAt > dateTimeEnd ? dateTimeEnd : ev.EndAt.Value) : (dateTimeNow < dateTimeEnd ? dateTimeNow : dateTimeEnd);

                var seconds = (evEnd - evStart).TotalSeconds;
                if (seconds > 0)
                {
                    if (ev.IsPowerOn)
                    {
                        onSeconds += seconds;
                        TotalUptimeSeconds += seconds;
                    }
                    else
                    {
                        offSeconds += seconds;
                        TotalDowntimeSeconds += seconds;
                    }
                }
            }
            Days.Add(new DayStat(dateTimeStart, onSeconds, offSeconds));
        }

        OutageCount = 0;
        MaxOutageSeconds = 0;

        foreach (var ev in events)
        {
            if (ev.IsPowerOn || ev.StartAt < filterDateTimeStart || ev.StartAt > filterDateTimeEnd) continue;

            OutageCount++;
            var evStart = ev.StartAt;
            var evEnd = ev.EndAt.HasValue ? (ev.EndAt > filterDateTimeEnd ? filterDateTimeEnd : ev.EndAt.Value) : filterDateTimeEnd;

            var seconds = (evEnd - evStart).TotalSeconds;
            if (seconds > MaxOutageSeconds) MaxOutageSeconds = seconds;
        }

        var periodSeconds = (filterDateTimeEnd - filterDateTimeStart).TotalSeconds;
        UptimePercent = periodSeconds > 0 ? TotalUptimeSeconds / periodSeconds * 100.0 : 0.0;
        AvgOutageSeconds = OutageCount > 0 ? TotalDowntimeSeconds / OutageCount : 0.0;
    }
}

public record DayStat(DateTime Date, double OnSeconds, double OffSeconds);
