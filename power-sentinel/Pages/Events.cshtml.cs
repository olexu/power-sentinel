using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PowerSentinel.Data;
using PowerSentinel.Models;
using System.Diagnostics;
using System.Globalization;

namespace PowerSentinel.Pages;

public class EventsModel : PageModel
{
    private readonly AppDbContext _db;
    public EventsModel(AppDbContext db) { _db = db; }

    public string DeviceId { get; private set; } = string.Empty;
    public DateTime FilterDate { get; private set; }
    public DeviceInfo DeviceInfo { get; private set; } = new DeviceInfo(string.Empty, string.Empty, null, null);
    public List<EventDisplay> DisplayEvents { get; private set; } = new();
    public TimeSpan TotalOnDuration { get; private set; }
    public TimeSpan TotalOffDuration { get; private set; }
    public double UptimePercent { get; private set; }
    public double DowntimePercent { get; private set; }

    public async Task OnGetAsync(string? deviceId, string? date)
    {
        // Redirect to index if no deviceId provided
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            Response.Redirect("/");
            return;
        }

        DeviceId = deviceId;
        var dateTimeNow = DateTime.Now;

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

        if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            FilterDate = dt.Date;
        else
            FilterDate = dateTimeNow.Date;

        var filterDateTimeStart = FilterDate.Date;
        var filterDateTimeEnd = filterDateTimeStart.AddDays(1);

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

        for (int i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            var fromDateTime = ev.Date < filterDateTimeStart ? filterDateTimeStart : ev.Date;

            DateTime toDateTime;
            if (i + 1 < events.Count)
            {
                var nextEventDate = events[i + 1].Date;
                toDateTime = nextEventDate < filterDateTimeEnd ? nextEventDate : filterDateTimeEnd;
            }
            else
            {
                toDateTime = dateTimeNow < filterDateTimeEnd ? dateTimeNow : filterDateTimeEnd;
            }
            if (toDateTime < fromDateTime)
                continue;
            DisplayEvents.Add(new EventDisplay(ev.IsPowerOn,
                ev.Date,
                toDateTime - fromDateTime));
        }

        TotalOnDuration = TimeSpan.Zero;
        TotalOffDuration = TimeSpan.Zero;

        foreach (var d in DisplayEvents)
        {
            if (d.IsPowerOn)
            {
                TotalOnDuration += d.DisplayDuration;
            }
            else
            {
                TotalOffDuration += d.DisplayDuration;
            }
        }

        var totalObserved = TotalOnDuration + TotalOffDuration;
        UptimePercent = totalObserved.TotalSeconds > 0 ? (TotalOnDuration.TotalSeconds / totalObserved.TotalSeconds) * 100.0 : 0.0;
        DowntimePercent = totalObserved.TotalSeconds > 0 ? (TotalOffDuration.TotalSeconds / totalObserved.TotalSeconds) * 100.0 : 0.0;
    }
}

public class EventDisplay
{
    public bool IsPowerOn { get; }
    public DateTime DisplayDate { get; }
    public TimeSpan DisplayDuration { get; }
    public EventDisplay(bool isPowerOn, DateTime ds, TimeSpan duration)
    {
        IsPowerOn = isPowerOn;
        DisplayDate = ds;
        DisplayDuration = duration;
    }
}
