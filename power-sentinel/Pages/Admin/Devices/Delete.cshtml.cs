using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PowerSentinel.Data;
using PowerSentinel.Models;

namespace PowerSentinel.Pages.Admin.Devices;

[Authorize(Policy = "AdminOnly")]
public class DeleteModel : PageModel
{
    private readonly AppDbContext _db;
    public DeleteModel(AppDbContext db) { _db = db; }

    [BindProperty]
    public Device? Device { get; set; }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (string.IsNullOrEmpty(id)) return RedirectToPage("Index");
        Device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Device == null) return RedirectToPage("Index");
        var existing = await _db.Devices.FirstOrDefaultAsync(d => d.Id == Device.Id);
        if (existing != null)
        {
            _db.Devices.Remove(existing);
            await _db.SaveChangesAsync();
        }
        return RedirectToPage("Index");
    }
}
