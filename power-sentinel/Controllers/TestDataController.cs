using Microsoft.AspNetCore.Mvc;
using PowerSentinel.Data;
using PowerSentinel.Models;

namespace PowerSentinel.Controllers;

[ApiController]
public class TestDataController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<TestDataController> _log;
    private readonly IWebHostEnvironment _env;

    public TestDataController(AppDbContext db, ILogger<TestDataController> log, IWebHostEnvironment env)
    {
        _db = db;
        _log = log;
        _env = env;
    }

    [HttpGet("api/testdata/generate")]
    public async Task<IActionResult> Generate(string? deviceId)
    {
        if (_env.IsProduction())
        {
            // disable test data endpoints in production
            return NotFound();
        }
        var rng = new Random();

        // Create or use provided device id
        var id = string.IsNullOrWhiteSpace(deviceId) ? Guid.NewGuid().ToString() : deviceId!;

        var device = await _db.Devices.FindAsync(id);
        if (device == null)
        {
            device = new Device { Id = id, Description = id };
            _db.Devices.Add(device);
            await _db.SaveChangesAsync();
        }

        // Generate events for last 2 months
        var endDate = DateTime.Now;
        var startDate = endDate.AddMonths(-2);

        var events = new List<Event>();

        DateTime currentStart = startDate;

        // Ensure we include both same-day and multi-day events
        bool createdSameDay = false, createdMultiDay = false;

        // Start power state randomly, then alternate for each event
        var nextIsOn = rng.Next(0, 2) == 0;

        while (currentStart < endDate)
        {
            // Decide event type: bias towards same-day events but include some multi-day
            var isMultiDay = rng.NextDouble() < 0.25; // 25% multi-day

            DateTime eventStart = currentStart; // start exactly at previous event end

            DateTime eventEnd;
            if (!isMultiDay)
            {
                // same-day: choose duration but ensure it doesn't cross midnight
                var endOfDay = new DateTime(eventStart.Year, eventStart.Month, eventStart.Day, 23, 59, 0, DateTimeKind.Utc);
                var minutesAvailable = (int)Math.Floor((endOfDay - eventStart).TotalMinutes);

                if (minutesAvailable < 1)
                {
                    // no room left on this day — treat as multi-day instead
                    var days = rng.Next(1, 3);
                    eventEnd = eventStart.AddDays(days).AddHours(rng.Next(0, 24)).AddMinutes(rng.Next(0, 60));
                    createdMultiDay = true;
                }
                else
                {
                    // pick duration between 30 minutes and up to available minutes (or up to 12 hours)
                    var maxMinutes = Math.Min(minutesAvailable, 12 * 60);
                    var minMinutes = Math.Min(30, maxMinutes);
                    var durationMinutes = rng.Next(minMinutes, maxMinutes + 1);
                    eventEnd = eventStart.AddMinutes(durationMinutes);
                    createdSameDay = true;
                }
            }
            else
            {
                // multi-day: duration 1 .. 5 days
                var days = rng.Next(1, 6);
                eventEnd = eventStart.AddDays(days).AddHours(rng.Next(0, 24)).AddMinutes(rng.Next(0, 60));
                createdMultiDay = true;
            }

            if (eventEnd > endDate) eventEnd = endDate;

            var evOn = new Event
            {
                DeviceId = id,
                IsPowerOn = nextIsOn,
                StartAt = eventStart,
                EndAt = eventEnd
            };

            events.Add(evOn);

            // Alternate for next event
            nextIsOn = !nextIsOn;

            // Next event starts exactly at previous end
            currentStart = eventEnd;
        }

        // Guarantee both kinds exist; if missing, add one small sample
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

        // Ensure chronological order and strict alternation: on only after off and vice-versa
        events = events.OrderBy(e => e.StartAt).ToList();
        for (int i = 1; i < events.Count; i++)
        {
            events[i].IsPowerOn = !events[i - 1].IsPowerOn;
        }

        _db.Events.AddRange(events);
        await _db.SaveChangesAsync();

        return Ok(new { DeviceId = id, EventsCreated = events.Count });
    }
}
