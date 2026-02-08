using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PowerSentinel.Data;
using PowerSentinel.Models;

namespace PowerSentinel.Pages.Admin.Events;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) { _db = db; }

    public List<Event> Events { get; private set; } = new();
    public List<string> DeviceIds { get; private set; } = new();

    public string? DeviceIdFilter { get; private set; }
    public int PageNumber { get; private set; } = 1;
    public int PageSize { get; private set; } = 25;
    public int TotalPages { get; private set; }
    public int TotalCount { get; private set; }

    public async Task OnGetAsync(string? deviceId, int pageNumber = 1, int pageSize = 25)
    {
        DeviceIdFilter = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId;
        PageNumber = Math.Max(1, pageNumber);
        PageSize = Math.Max(1, pageSize);

        DeviceIds = await _db.Devices.OrderBy(d => d.Id).Select(d => d.Id).ToListAsync();

        var query = _db.Events.AsQueryable();
        if (!string.IsNullOrWhiteSpace(DeviceIdFilter))
            query = query.Where(e => e.DeviceId == DeviceIdFilter);

        TotalCount = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(TotalCount / (double)PageSize);

        Events = await query
            .OrderByDescending(e => e.StartAt)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }
}
