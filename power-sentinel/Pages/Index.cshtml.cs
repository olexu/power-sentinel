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
        var filterDateTimeEnd = filterDateTimeStart.AddMonths(1);

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
                e.DeviceId == SelectedDeviceId && 
                ((e.StartAt >= filterDateTimeStart && e.StartAt < filterDateTimeEnd) || (e.EndAt != null && e.EndAt >= filterDateTimeStart && e.EndAt < filterDateTimeEnd)))
            .ToListAsync();

        for (int d = 1; d <= DateTime.DaysInMonth(Year, Month); d++)
        {
            var dayStart = new DateTime(Year, Month, d, 0, 0, 0);
            var dayEnd = dayStart.AddDays(1);

            double onSeconds = 0;
            double offSeconds = 0;

            foreach (var ev in events)
            {
                var evStart = ev.StartAt < dayStart ? dayStart : ev.StartAt;
                var evEnd = ev.EndAt.HasValue ? ev.EndAt > dayEnd ? dayEnd : ev.EndAt.Value : dateTimeNow;

                var seconds = (evEnd - evStart).TotalSeconds;
                if (seconds > 0)
                {
                    if (ev.IsPowerOn)
                        onSeconds += seconds;
                    else
                        offSeconds += seconds;
                }
            }
            Days.Add(new DayStat(dayStart, onSeconds, offSeconds));
        }



        TotalDowntimeSeconds = 0;
        TotalUptimeSeconds = 0;
        OutageCount = 0;
        MaxOutageSeconds = 0;

        var outageByDevice = new Dictionary<string, double>();
        var periodEnd = dateTimeNow < filterDateTimeEnd ? dateTimeNow : filterDateTimeEnd; // month up to current day/time
        var periodSeconds = (periodEnd - filterDateTimeStart).TotalSeconds;
        var elapsedDays = (periodEnd - filterDateTimeStart).TotalDays;

        foreach (var ev in events)
        {
            var evStart = ev.StartAt;
            var evEnd = ev.EndAt.HasValue ? ev.EndAt.Value : dateTimeNow;
            if (evEnd <= filterDateTimeStart || evStart >= periodEnd) continue;

            var overlapStart = evStart < filterDateTimeStart ? filterDateTimeStart : evStart;
            var overlapEnd = evEnd > periodEnd ? periodEnd : evEnd;
            var seconds = (overlapEnd - overlapStart).TotalSeconds;
            if (seconds <= 0) continue;

            if (ev.IsPowerOn) TotalUptimeSeconds += seconds;
            else
            {
                TotalDowntimeSeconds += seconds;
                OutageCount++;
                var did = ev.DeviceId ?? string.Empty;
                if (!outageByDevice.ContainsKey(did)) outageByDevice[did] = 0;
                outageByDevice[did] += seconds;
                if (seconds > MaxOutageSeconds) MaxOutageSeconds = seconds;
            }
        }

        UptimePercent = periodSeconds > 0 ? TotalUptimeSeconds / periodSeconds * 100.0 : 0.0;
        AvgOutageSeconds = OutageCount > 0 ? TotalDowntimeSeconds / OutageCount : 0.0;


    }
}
