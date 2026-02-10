using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PowerSentinel.Data;
using PowerSentinel.Models;
using System.Text;
using System.IO;
using System.Linq;
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

    public async Task<IActionResult> OnPostGenerateAsync(string? deviceId)
    {
        try
        {
            var rng = new Random();

            var id = string.IsNullOrWhiteSpace(deviceId) ? $"gen-{Guid.NewGuid():N}" : deviceId!;

            // ensure device exists
            var device = await _db.Devices.FindAsync(id);
            if (device == null)
            {
                device = new Device { Id = id, Description = "Generated device" };
                await _db.Devices.AddAsync(device);
                await _db.SaveChangesAsync();
            }

            // generate events over the last 30 days
            const int eventsCount = 500;
            var days = 30;
            var start = DateTime.UtcNow.AddDays(-days);
            var totalSeconds = TimeSpan.FromDays(days).TotalSeconds;

            var events = new List<Event>(eventsCount);
            bool lastState = rng.Next(2) == 0;

            for (int i = 0; i < eventsCount; i++)
            {
                // evenly space then add some jitter
                var frac = (double)i / Math.Max(1, eventsCount - 1);
                var seconds = frac * totalSeconds + rng.NextDouble() * 3600.0 - 1800.0; // +/-30m jitter
                var date = start.AddSeconds(seconds);

                // small chance to flip state; otherwise keep the last state
                if (rng.NextDouble() < 0.2)
                    lastState = !lastState;

                events.Add(new Event
                {
                    DeviceId = id,
                    IsPowerOn = lastState,
                    Date = date,
                });
            }

            await _db.Events.AddRangeAsync(events);
            var saved = await _db.SaveChangesAsync();

            StatusMessage = $"Generated {events.Count} events for device {id} (DB changes: {saved}).";
        }
        catch (Exception ex)
        {
            StatusMessage = "Generate failed: " + ex.Message;
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
