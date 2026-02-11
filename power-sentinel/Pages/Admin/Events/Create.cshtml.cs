using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PowerSentinel.Data;
using PowerSentinel.Models;
using System.Linq;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace PowerSentinel.Pages.Admin.Events;

[Authorize(Policy = "AdminOnly")]
public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    public CreateModel(AppDbContext db) { _db = db; }

    [BindProperty]
    public Event Event { get; set; } = new();

    public List<Device> Devices { get; set; } = new();

    public void OnGet()
    {
        var now = DateTime.Now;
        Event.Date = new DateTime(now.Ticks - (now.Ticks % TimeSpan.TicksPerMinute), now.Kind);
        Devices = _db.Devices.OrderBy(d => d.Id).ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        _db.Events.Add(Event);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
