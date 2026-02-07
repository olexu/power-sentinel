using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PowerSentinel.Data;
using PowerSentinel.Models;

namespace PowerSentinel.Pages.Admin.Events;

[Authorize(Policy = "AdminOnly")]
public class DeleteModel : PageModel
{
    private readonly AppDbContext _db;
    public DeleteModel(AppDbContext db) { _db = db; }

    [BindProperty]
    public Event? Event { get; set; }

    public async Task<IActionResult> OnGetAsync(long id)
    {
        Event = await _db.Events.FirstOrDefaultAsync(e => e.Id == id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Event == null) return RedirectToPage("Index");
        var existing = await _db.Events.FirstOrDefaultAsync(e => e.Id == Event.Id);
        if (existing != null)
        {
            _db.Events.Remove(existing);
            await _db.SaveChangesAsync();
        }
        return RedirectToPage("Index");
    }
}
