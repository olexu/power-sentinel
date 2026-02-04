using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PowerSentinel.Data;
using PowerSentinel.Models;
using System.Globalization;

namespace PowerSentinel.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) { _db = db; }

    public int Year { get; private set; }
    public int Month { get; private set; }
    public record DayStat(DateTime Date, double OnSeconds, double OffSeconds);
    public List<DayStat> Days { get; private set; } = new();
    public string MonthName { get; private set; } = string.Empty;
    public double TotalDowntimeSeconds { get; private set; }
    public double TotalUptimeSeconds { get; private set; }
    public int OutageCount { get; private set; }
    public double AvgOutageSeconds { get; private set; }
    public double MaxOutageSeconds { get; private set; }
    public double UptimePercent { get; private set; }
    public List<Device> Devices { get; private set; } = new();
    public string? SelectedDeviceId { get; private set; }
    public Dictionary<string, bool?> DeviceStatuses { get; private set; } = new();

    public async Task OnGetAsync(int? year, int? month, string? deviceId)
    {
        var dateTimeNow = DateTime.Now;

        Year = year ?? dateTimeNow.Year;
        Month = month ?? dateTimeNow.Month;
        SelectedDeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId;

        var filterDateTimeStart = new DateTime(Year, Month, 1, 0, 0, 0);
        var monthEnd = filterDateTimeStart.AddMonths(1);
        var filterDateTimeEnd = dateTimeNow < monthEnd ? dateTimeNow : monthEnd;

        MonthName = filterDateTimeStart.ToString("MMMM yyyy");

        Devices = await _db.Devices.OrderBy(d => d.Id).ToListAsync();

        if (SelectedDeviceId == null && Devices.Count > 0)
            SelectedDeviceId = Devices[0].Id;

        foreach (var d in Devices)
        {
            var lastEv = await _db.Events.Where(e => e.DeviceId == d.Id).OrderByDescending(e => e.StartAt).FirstOrDefaultAsync();
            DeviceStatuses[d.Id] = lastEv != null && lastEv.EndAt == null ? (bool?)lastEv.IsPowerOn : null;
        }

        var events = await _db.Events.AsQueryable()
            .Where(e =>
                e.DeviceId == SelectedDeviceId && (
                    (e.StartAt >= filterDateTimeStart && e.StartAt < filterDateTimeEnd) ||
                    (e.EndAt == null) ||
                    (e.EndAt != null && e.EndAt >= filterDateTimeStart && e.EndAt < filterDateTimeEnd)
                ))
                .OrderBy(e => e.StartAt)
            .ToListAsync();

        TotalUptimeSeconds = 0;
        TotalDowntimeSeconds = 0;

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
