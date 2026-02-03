using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PowerSentinel.Data;
using PowerSentinel.Models;
using System.Globalization;

namespace PowerSentinel.Pages;

public class EventsModel : PageModel
{
    private readonly AppDbContext _db;
    public EventsModel(AppDbContext db) { _db = db; }

    public List<Device> Devices { get; private set; } = new();
    public Dictionary<string, bool?> DeviceStatuses { get; private set; } = new();
    public string? SelectedDeviceId { get; private set; }
    public DateTime FilterDate { get; private set; }
    public List<EventDisplay> DisplayEvents { get; private set; } = new();

    public async Task OnGetAsync(string? deviceId, string? date)
    {
        SelectedDeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId;
        Devices = await _db.Devices.OrderBy(d => d.Id).ToListAsync();

        foreach (var d in Devices)
        {
            var lastEv = await _db.Events.Where(e => e.DeviceId == d.Id).OrderByDescending(e => e.StartAt).FirstOrDefaultAsync();
            DeviceStatuses[d.Id] = lastEv != null && lastEv.EndAt == null ? (bool?)lastEv.IsPowerOn : null;
        }

        if (SelectedDeviceId == null && Devices.Count > 0)
            SelectedDeviceId = Devices[0].Id;

        var dateTimeNow = DateTime.Now;

        if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            FilterDate = dt.Date;
        else
            FilterDate = dateTimeNow.Date;

        var query = _db.Events.AsQueryable().Where(e => e.DeviceId == SelectedDeviceId);

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
