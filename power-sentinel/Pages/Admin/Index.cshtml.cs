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

    public async Task<IActionResult> OnPostGenerateAsync(string? deviceId)
    {
        var rng = new Random();

        var id = string.IsNullOrWhiteSpace(deviceId) ? Guid.NewGuid().ToString() : deviceId!;

        var device = await _db.Devices.FindAsync(id);
        if (device == null)
        {
            device = new Device { Id = id, Description = id };
            _db.Devices.Add(device);
            await _db.SaveChangesAsync();
        }

        var endDate = DateTime.Now;
        var startDate = endDate.AddMonths(-2);

        var events = new List<Event>();

        DateTime currentStart = startDate;
        bool createdSameDay = false, createdMultiDay = false;
        var nextIsOn = rng.Next(0, 2) == 0;

        while (currentStart < endDate)
        {
            var isMultiDay = rng.NextDouble() < 0.25;
            DateTime eventStart = currentStart;
            DateTime eventEnd;
            if (!isMultiDay)
            {
                var endOfDay = new DateTime(eventStart.Year, eventStart.Month, eventStart.Day, 23, 59, 0, DateTimeKind.Utc);
                var minutesAvailable = (int)Math.Floor((endOfDay - eventStart).TotalMinutes);

                if (minutesAvailable < 1)
                {
                    var days = rng.Next(1, 3);
                    eventEnd = eventStart.AddDays(days).AddHours(rng.Next(0, 24)).AddMinutes(rng.Next(0, 60));
                    createdMultiDay = true;
                }
                else
                {
                    var maxMinutes = Math.Min(minutesAvailable, 12 * 60);
                    var minMinutes = Math.Min(30, maxMinutes);
                    var durationMinutes = rng.Next(minMinutes, maxMinutes + 1);
                    eventEnd = eventStart.AddMinutes(durationMinutes);
                    createdSameDay = true;
                }
            }
            else
            {
                var days = rng.Next(1, 6);
                eventEnd = eventStart.AddDays(days).AddHours(rng.Next(0, 24)).AddMinutes(rng.Next(0, 60));
                createdMultiDay = true;
            }

            if (eventEnd > endDate) eventEnd = endDate;

            events.Add(new Event { DeviceId = id, IsPowerOn = nextIsOn, StartAt = eventStart, EndAt = eventEnd });

            nextIsOn = !nextIsOn;
            currentStart = eventEnd;
        }

        if (!createdMultiDay)
        {
            var sampleStart = endDate.AddDays(-10);
            var sampleEnd = sampleStart.AddDays(2);
            events.Add(new Event { DeviceId = id, IsPowerOn = false, StartAt = sampleStart, EndAt = sampleEnd });
        }
        if (!createdSameDay)
        {
            var sampleStart = endDate.AddDays(-1).AddHours(9);
            var sampleEnd = sampleStart.AddHours(3);
            events.Add(new Event { DeviceId = id, IsPowerOn = true, StartAt = sampleStart, EndAt = sampleEnd });
        }

        events = events.OrderBy(e => e.StartAt).ToList();
        for (int i = 1; i < events.Count; i++)
        {
            events[i].IsPowerOn = !events[i - 1].IsPowerOn;
        }

        await _db.Events.AddRangeAsync(events);
        var added = await _db.SaveChangesAsync();

        StatusMessage = $"Generated {events.Count} events for device {id} (DB changes: {added}).";
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
