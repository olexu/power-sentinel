using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PowerSentinel.Data;
using PowerSentinel.Models;

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
                    .OrderByDescending(e => e.Date)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync();
        var lastEvent = deviceInfo?.lastEvent;
        bool? isOn = lastEvent != null ? lastEvent.IsPowerOn : null;
        TimeSpan? timeSpan = lastEvent != null ? dateTimeNow - lastEvent.Date : null;
        DeviceInfo = new DeviceInfo(deviceInfo?.device.Id ?? string.Empty, deviceInfo?.device.Description ?? deviceInfo?.device.Id ?? string.Empty, isOn, timeSpan);

        var eventsInRange = await _db.Events
            .Where(e => e.DeviceId == DeviceId && e.Date >= filterDateTimeStart && e.Date < filterDateTimeEnd)
            .OrderBy(e => e.Date)
            .ToListAsync();

        var lastEventBeforeRange = await _db.Events
            .Where(e => e.DeviceId == DeviceId && e.Date < filterDateTimeStart)
            .OrderByDescending(e => e.Date)
            .FirstOrDefaultAsync();

        var events = new List<Event>(eventsInRange);
        if (lastEventBeforeRange != null)
            events.Insert(0, lastEventBeforeRange);

        double TotalUptimeSeconds = 0;
        double TotalDowntimeSeconds = 0;

        for (int d = 1; d <= DateTime.DaysInMonth(Year, Month); d++)
        {
            var dateTimeStart = new DateTime(Year, Month, d, 0, 0, 0);
            var dateTimeEnd = dateTimeStart.AddDays(1);

            double onSeconds = 0;
            double offSeconds = 0;

            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];

                DateTime nextDate = (i + 1 < events.Count)
                    ? events[i + 1].Date
                    : (dateTimeNow < filterDateTimeEnd ? dateTimeNow : filterDateTimeEnd);

                if (ev.Date >= dateTimeEnd || nextDate <= dateTimeStart) continue;

                var evStart = ev.Date < dateTimeStart ? dateTimeStart : ev.Date;
                var evEnd = nextDate > dateTimeEnd ? dateTimeEnd : nextDate;

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

        for (int i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            if (ev.IsPowerOn || ev.Date < filterDateTimeStart || ev.Date >= filterDateTimeEnd) continue;

            DateTime nextDate = (i + 1 < events.Count) ? events[i + 1].Date : filterDateTimeEnd;
            var evStart = ev.Date;
            var evEnd = nextDate > filterDateTimeEnd ? filterDateTimeEnd : nextDate;

            var seconds = (evEnd - evStart).TotalSeconds;
            if (seconds > 0)
            {
                OutageCount++;
                if (seconds > MaxOutageSeconds) MaxOutageSeconds = seconds;
            }
        }

        var periodSeconds = (filterDateTimeEnd - filterDateTimeStart).TotalSeconds;
        UptimePercent = periodSeconds > 0 ? TotalUptimeSeconds / periodSeconds * 100.0 : 0.0;
        AvgOutageSeconds = OutageCount > 0 ? TotalDowntimeSeconds / OutageCount : 0.0;
    }
}

public record DayStat(DateTime Date, double OnSeconds, double OffSeconds);
