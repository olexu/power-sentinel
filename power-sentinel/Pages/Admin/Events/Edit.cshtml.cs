using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PowerSentinel.Data;
using PowerSentinel.Models;

namespace PowerSentinel.Pages.Admin.Events;

[Authorize(Policy = "AdminOnly")]
public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    public EditModel(AppDbContext db) { _db = db; }

    [BindProperty]
    public Event Event { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(long id)
    {
        var e = await _db.Events.FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return RedirectToPage("Index");
        Event = e;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var existing = await _db.Events.FirstOrDefaultAsync(x => x.Id == Event.Id);
        if (existing == null) return RedirectToPage("Index");
        existing.DeviceId = Event.DeviceId;
        existing.IsPowerOn = Event.IsPowerOn;
        existing.StartAt = Event.StartAt;
        existing.EndAt = Event.EndAt;
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
