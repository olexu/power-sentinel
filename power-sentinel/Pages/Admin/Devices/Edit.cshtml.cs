using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PowerSentinel.Data;
using PowerSentinel.Models;

namespace PowerSentinel.Pages.Admin.Devices;

[Authorize(Policy = "AdminOnly")]
public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    public EditModel(AppDbContext db) { _db = db; }

    [BindProperty]
    public Device Device { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (string.IsNullOrEmpty(id)) return RedirectToPage("Index");
        var d = await _db.Devices.FirstOrDefaultAsync(x => x.Id == id);
        if (d == null) return RedirectToPage("Index");
        Device = d;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var existing = await _db.Devices.FirstOrDefaultAsync(x => x.Id == Device.Id);
        if (existing == null) return RedirectToPage("Index");
        existing.Description = Device.Description;
        existing.Heartbeat = Device.Heartbeat;
        existing.HeartbeatKey = Device.HeartbeatKey;
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
