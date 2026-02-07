using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PowerSentinel.Data;
using PowerSentinel.Models;

namespace PowerSentinel.Pages.Admin.Events;

[Authorize(Policy = "AdminOnly")]
public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    public CreateModel(AppDbContext db) { _db = db; }

    [BindProperty]
    public Event Event { get; set; } = new();

    public void OnGet() { Event.StartAt = DateTime.Now; }

    public async Task<IActionResult> OnPostAsync()
    {
        _db.Events.Add(Event);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
