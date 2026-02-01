using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PowerSentinel.Data;
using PowerSentinel.Models;
using System.Globalization;

namespace PowerSentinel.Pages;

public class EventsModel : PageModel
{
    private readonly AppDbContext _db;

    public EventsModel(AppDbContext db)
    {
        _db = db;
    }

    public List<Device> Devices { get; private set; } = new();
    public Dictionary<string, bool?> DeviceStatuses { get; private set; } = new();

    public string? SelectedDeviceId { get; private set; }
    // optional date filter (yyyy-MM-dd)
    public DateTime? DateFilter { get; private set; }

    public List<Event> EventsList { get; private set; } = new();
    public List<EventDisplay> DisplayEvents { get; private set; } = new();
    public int PageNumber { get; private set; }
    public int PageSize { get; private set; } = 20;
    public int TotalCount { get; private set; }

    public async Task OnGetAsync(string? deviceId, string? date, int? page, int? pageSize)
    {
        SelectedDeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId;
        PageNumber = Math.Max(1, page ?? 1);
        if (pageSize.HasValue && pageSize.Value > 0) PageSize = pageSize.Value;

        Devices = await _db.Devices.OrderBy(d => d.Id).ToListAsync();

        // per-device latest status
        foreach (var d in Devices)
        {
            var lastEv = await _db.Events.Where(e => e.DeviceId == d.Id).OrderByDescending(e => e.StartAt).FirstOrDefaultAsync();
            DeviceStatuses[d.Id] = lastEv != null && lastEv.EndAt == null ? (bool?)lastEv.IsPowerOn : null;
        }

        if (!string.IsNullOrWhiteSpace(date))
        {
            if (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            {
                DateFilter = dt.Date;
            }
        }

        var query = _db.Events.AsQueryable();
        if (SelectedDeviceId != null)
            query = query.Where(e => e.DeviceId == SelectedDeviceId);

        DateTime? dayStart = null;
        DateTime? dayEnd = null;
        if (DateFilter.HasValue)
        {
            dayStart = DateFilter.Value.Date;
            dayEnd = dayStart.Value.AddDays(1);
            // show only events that either start on the selected day OR finish on the selected day
            query = query.Where(e => (e.StartAt >= dayStart && e.StartAt < dayEnd)
                                      || (e.EndAt != null && e.EndAt >= dayStart && e.EndAt < dayEnd));
        }

        TotalCount = await query.CountAsync();

        // sort events ascending by StartAt
        EventsList = await query.OrderBy(e => e.StartAt)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
        // prepare display list without modifying DB entities
        DisplayEvents.Clear();
        if (dayStart.HasValue && dayEnd.HasValue)
        {
            foreach (var ev in EventsList)
            {
                var ds = ev.StartAt < dayStart.Value ? dayStart.Value : ev.StartAt;
                DateTime? de = null;
                if (ev.EndAt == null)
                {
                    // ongoing -> assume finished at midnight of next day for display
                    de = dayEnd.Value;
                }
                else
                {
                    de = ev.EndAt > dayEnd.Value ? dayEnd.Value : ev.EndAt;
                }

                DisplayEvents.Add(new EventDisplay(ev, ds, de));
            }
        }
        else
        {
            foreach (var ev in EventsList)
            {
                DisplayEvents.Add(new EventDisplay(ev, ev.StartAt, ev.EndAt));
            }
        }
    }
}

public class EventDisplay
{
    public Event Source { get; }
    public DateTime DisplayStart { get; }
    public DateTime? DisplayEnd { get; }

    public EventDisplay(Event src, DateTime ds, DateTime? de)
    {
        Source = src;
        DisplayStart = ds;
        DisplayEnd = de;
    }
}
