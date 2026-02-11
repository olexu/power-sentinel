using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PowerSentinel.Data;
using PowerSentinel.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PowerSentinel.Pages.Admin.Subscribers;

[Authorize(Policy = "AdminOnly")]
public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    public CreateModel(AppDbContext db) { _db = db; }

    [BindProperty]
    public Subscriber Subscriber { get; set; } = new();

    public List<Device> Devices { get; set; } = new();

    public void OnGet()
    {
        Devices = _db.Devices.OrderBy(d => d.Id).ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Subscriber.DeviceId)) Subscriber.DeviceId = null;
        _db.Subscribers.Add(Subscriber);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
