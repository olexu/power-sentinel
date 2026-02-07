using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PowerSentinel.Data;
using PowerSentinel.Models;
using System.Text;
using System.Text.Json;

namespace PowerSentinel.Pages.Admin;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) { _db = db; }

    [TempData]
    public string StatusMessage { get; set; } = string.Empty;

    public async Task OnGetAsync()
    {
        // no-op, page shows admin actions
        await Task.CompletedTask;
    }

    public async Task<IActionResult> OnPostExportAsync()
    {
        var events = await _db.Events.OrderByDescending(e => e.StartAt).ToListAsync();

        var export = events.Select(e => new {
            e.DeviceId,
            e.IsPowerOn,
            e.StartAt,
            e.EndAt
        }).ToList();

        var opts = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(export, opts);
        var bytes = Encoding.UTF8.GetBytes(json);

        return File(bytes, "application/json", "events.json");
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
                StartAt = i.StartAt,
                EndAt = i.EndAt
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
        public DateTime StartAt { get; set; }
        public DateTime? EndAt { get; set; }
    }
}
