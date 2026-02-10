using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    [TempData]
    public string StatusMessage { get; set; } = string.Empty;

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
            .OrderByDescending(e => e.Date)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostExportAsync(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            StatusMessage = "Please select a device to export.";
            return RedirectToPage();
        }

        var events = await _db.Events.Where(e => e.DeviceId == deviceId).OrderByDescending(e => e.Date).ToListAsync();

        var export = events.Select(e => new
        {
            e.DeviceId,
            e.IsPowerOn,
            Date = new DateTime(e.Date.Ticks - (e.Date.Ticks % TimeSpan.TicksPerSecond), e.Date.Kind),
        }).ToList();

        var opts = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(export, opts);
        var bytes = Encoding.UTF8.GetBytes(json);

        // sanitize filename
        var invalid = Path.GetInvalidFileNameChars();
        var safeId = string.Concat(deviceId.Where(ch => !invalid.Contains(ch)));
        var filename = string.IsNullOrEmpty(safeId) ? "events.json" : safeId + ".json";

        return File(bytes, "application/json", filename);
    }

    public async Task<IActionResult> OnPostImportAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            StatusMessage = "No file uploaded.";
            return RedirectToPage();
        }

        try
        {
            using var stream = file.OpenReadStream();
            var importList = await JsonSerializer.DeserializeAsync<List<ImportedEvent>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (importList == null || importList.Count == 0)
            {
                StatusMessage = "No events found in uploaded file.";
                return RedirectToPage();
            }

            var toAdd = importList.Select(i => new Event {
                DeviceId = i.DeviceId ?? string.Empty,
                IsPowerOn = i.IsPowerOn,
                Date = i.Date,
            }).ToList();

            await _db.Events.AddRangeAsync(toAdd);
            var imported = await _db.SaveChangesAsync();

            StatusMessage = $"Imported {toAdd.Count} events (DB changes: {imported}).";
        }
        catch (Exception ex)
        {
            StatusMessage = "Import failed: " + ex.Message;
        }

        return RedirectToPage();
    }

    private class ImportedEvent
    {
        public string? DeviceId { get; set; }
        public bool IsPowerOn { get; set; }
        public DateTime Date { get; set; }
    }

}
