using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PowerSentinel.Data;
using PowerSentinel.Models;

namespace PowerSentinel.Pages.Admin.Subscribers;

[Authorize(Policy = "AdminOnly")]
public class DeleteModel : PageModel
{
    private readonly AppDbContext _db;
    public DeleteModel(AppDbContext db) { _db = db; }

    [BindProperty]
    public Subscriber? Subscriber { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Subscriber = await _db.Subscribers.FirstOrDefaultAsync(s => s.Id == id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Subscriber == null) return RedirectToPage("Index");
        var existing = await _db.Subscribers.FirstOrDefaultAsync(s => s.Id == Subscriber.Id);
        if (existing != null)
        {
            _db.Subscribers.Remove(existing);
            await _db.SaveChangesAsync();
        }
        return RedirectToPage("Index");
    }
}
