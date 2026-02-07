using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PowerSentinel.Data;
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
                    .OrderByDescending(e => e.StartAt)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync();
        var lastEvent = deviceInfo?.lastEvent;
        bool? isOn = lastEvent != null && lastEvent.EndAt == null ? lastEvent.IsPowerOn : null;
        TimeSpan? timeSpan = lastEvent != null && lastEvent.EndAt == null ? dateTimeNow - lastEvent.StartAt : null;
        DeviceInfo = new DeviceInfo(deviceInfo?.device.Id ?? string.Empty, deviceInfo?.device.Description ?? deviceInfo?.device.Id ?? string.Empty, isOn, timeSpan);

        if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            FilterDate = dt.Date;
        else
            FilterDate = dateTimeNow.Date;

        var query = _db.Events.AsQueryable().Where(e => e.DeviceId == DeviceId);

        var filterDateTimeStart = FilterDate.Date;
        var filterDateTimeEnd = filterDateTimeStart.AddDays(1);

        query = query.Where(e => (e.StartAt >= filterDateTimeStart && e.StartAt < filterDateTimeEnd) || (e.EndAt != null && e.EndAt >= filterDateTimeStart && e.EndAt < filterDateTimeEnd));

        var events = await query.OrderBy(e => e.StartAt).ToListAsync();

        foreach (var ev in events)
        {
            var fromDateTime = ev.StartAt < filterDateTimeStart ? filterDateTimeStart.Date : ev.StartAt;
            var toTime = ev.EndAt.HasValue ? (ev.EndAt.Value < filterDateTimeEnd ? ev.EndAt.Value : filterDateTimeEnd) : dateTimeNow;

            DisplayEvents.Add(new EventDisplay(ev.IsPowerOn,
                ev.StartAt,
                ev.EndAt,
                toTime - fromDateTime));
        }
    }
}

public class EventDisplay
{
    public bool IsPowerOn { get; }
    public DateTime DisplayStart { get; }
    public DateTime? DisplayEnd { get; }
    public TimeSpan DisplayDuration { get; }
    public EventDisplay(bool isPowerOn, DateTime ds, DateTime? de, TimeSpan duration)
    {
        IsPowerOn = isPowerOn;
        DisplayStart = ds;
        DisplayEnd = de;
        DisplayDuration = duration;
    }
}
