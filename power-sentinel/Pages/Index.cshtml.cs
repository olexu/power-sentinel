using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PowerSentinel.Data;
using PowerSentinel.Models;
using System.Globalization;

namespace PowerSentinel.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db)
    {
        _db = db;
    }

    public record DayStat(DateTime Date, double OnSeconds, double OffSeconds);

    public List<DayStat> Days { get; private set; } = new();
    public int Year { get; private set; }
    public int Month { get; private set; }
    public string MonthName { get; private set; } = string.Empty;

    // Monthly summary metrics
    public double TotalDowntimeSeconds { get; private set; }
    public double TotalUptimeSeconds { get; private set; }
    public int OutageCount { get; private set; }
    public double AvgOutageSeconds { get; private set; }
    public double MaxOutageSeconds { get; private set; }
    // retained for internal use but not shown directly in new summary
    public double UptimePercent { get; private set; }


    // Devices for selector
    public List<Device> Devices { get; private set; } = new();
    public string? SelectedDeviceId { get; private set; }
    // true = on, false = off, null = unknown
    public bool? SelectedDeviceIsOn { get; private set; }
    // per-device current state (true = on, false = off, null = unknown)
    public Dictionary<string, bool?> DeviceStatuses { get; private set; } = new();

    public async Task OnGetAsync(int? year, int? month, string? deviceId)
    {
        var now = DateTime.Now;

        Year = year ?? now.Year;
        Month = month ?? now.Month;
        SelectedDeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId;

        var monthStart = new DateTime(Year, Month, 1, 0, 0, 0);
        var monthEnd = monthStart.AddMonths(1);

        Devices = await _db.Devices.OrderBy(d => d.Id).ToListAsync();

        if (SelectedDeviceId == null && Devices.Count > 0)
            SelectedDeviceId = Devices[0].Id;

        DeviceStatuses = new Dictionary<string, bool?>();
        if (Devices.Count > 0)
        {
            var deviceIds = Devices.Select(d => d.Id).ToList();
            var lastEvents = await _db.Events
                .Where(e => deviceIds.Contains(e.DeviceId))
                .GroupBy(e => e.DeviceId)
                .Select(g => g.OrderByDescending(e => e.StartAt).FirstOrDefault())
                .ToListAsync();

            foreach (var d in Devices)
            {
                var lastEv = lastEvents.FirstOrDefault(e => e != null && e.DeviceId == d.Id);
                DeviceStatuses[d.Id] = lastEv != null && lastEv.EndAt == null ? (bool?)lastEv.IsPowerOn : null;
            }

            if (SelectedDeviceId != null)
            {
                SelectedDeviceIsOn = DeviceStatuses.ContainsKey(SelectedDeviceId) ? DeviceStatuses[SelectedDeviceId] : null;
            }
        }

        MonthName = monthStart.ToString("MMMM yyyy");

        // get events that intersect the month range (EndAt null means ongoing)
        var eventsQuery = _db.Events
            .Where(e => (e.EndAt == null || e.EndAt.Value >= monthStart) && e.StartAt < monthEnd);

        if (SelectedDeviceId != null)
            eventsQuery = eventsQuery.Where(e => e.DeviceId == SelectedDeviceId);

        var events = await eventsQuery.ToListAsync();

        // Compute monthly metrics (for month up to now)
        TotalDowntimeSeconds = 0;
        TotalUptimeSeconds = 0;
        OutageCount = 0;
        MaxOutageSeconds = 0;
        var outageByDevice = new Dictionary<string, double>();

        var periodEnd = now < monthEnd ? now : monthEnd; // month up to current day/time
        var periodSeconds = (periodEnd - monthStart).TotalSeconds;
        var elapsedDays = (periodEnd - monthStart).TotalDays;

        foreach (var ev in events)
        {
            var evStart = ev.StartAt;
            var evEnd = ev.EndAt.HasValue ? ev.EndAt.Value : now;
            if (evEnd <= monthStart || evStart >= periodEnd) continue;

            var overlapStart = evStart < monthStart ? monthStart : evStart;
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

        int daysInMonth = DateTime.DaysInMonth(Year, Month);

        for (int d = 1; d <= daysInMonth; d++)
        {
            var dayStart = new DateTime(Year, Month, d, 0, 0, 0);
            var dayEnd = dayStart.AddDays(1);

            double onSeconds = 0;
            double offSeconds = 0;

            foreach (var ev in events)
            {
                var evStart = ev.StartAt;
                var evEnd = ev.EndAt.HasValue ? ev.EndAt.Value : now;

                if (evEnd <= dayStart || evStart >= dayEnd) continue;

                var overlapStart = evStart < dayStart ? dayStart : evStart;
                var overlapEnd = evEnd > dayEnd ? dayEnd : evEnd;
                var seconds = (overlapEnd - overlapStart).TotalSeconds;
                if (seconds <= 0) continue;

                if (ev.IsPowerOn)
                    onSeconds += seconds;
                else
                    offSeconds += seconds;
            }

            Days.Add(new DayStat(dayStart, onSeconds, offSeconds));
        }
    }
}
