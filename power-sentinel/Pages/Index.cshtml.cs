using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PowerSentinel.Data;

namespace PowerSentinel.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) { _db = db; }

    public List<DeviceInfo> Devices { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var dateTimeNow = DateTime.Now;

        var deviceEvents = await _db.Devices
            .Select(d => new {
                device = d,
                lastEvent = _db.Events
                    .Where(e => e.DeviceId == d.Id)
                    .OrderByDescending(e => e.StartAt)
                    .FirstOrDefault()
            })
            .ToListAsync();

        foreach (var item in deviceEvents)
        {
            var ev = item.lastEvent;
            bool? isOn = ev != null && ev.EndAt == null ? ev.IsPowerOn : null;
            TimeSpan? timeSpan = ev != null && ev.EndAt == null ? dateTimeNow - ev.StartAt : null;
            Devices.Add(new DeviceInfo(item.device.Id, item.device.Description ?? item.device.Id, isOn, timeSpan));
        }
    }
}

public class DeviceInfo
{
    public string Id { get; }
    public string Description { get; }
    public bool? IsOn { get; }
    public TimeSpan? TimeSpan { get; }
    public DeviceInfo(string id, string description, bool? isOn, TimeSpan? timeSpan)
    {
        Id = id;
        Description = description;
        IsOn = isOn;
        TimeSpan = timeSpan;
    }
}
