using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PowerSentinel.Data;
using PowerSentinel.Models;

namespace PowerSentinel.Pages.Admin.Subscribers;

[Authorize(Policy = "AdminOnly")]
public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    public EditModel(AppDbContext db) { _db = db; }

    [BindProperty]
    public Subscriber Subscriber { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var s = await _db.Subscribers.FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return RedirectToPage("Index");
        Subscriber = s;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var existing = await _db.Subscribers.FirstOrDefaultAsync(x => x.Id == Subscriber.Id);
        if (existing == null) return RedirectToPage("Index");
        existing.ChatId = Subscriber.ChatId;
        existing.DeviceId = Subscriber.DeviceId;
        existing.IsActive = Subscriber.IsActive;
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
