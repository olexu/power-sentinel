using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PowerSentinel.Data;
using PowerSentinel.Models;

namespace PowerSentinel.Pages.Admin.Subscribers;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) { _db = db; }

    public List<Subscriber> Subscribers { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Subscribers = await _db.Subscribers.OrderBy(s => s.Id).ToListAsync();
    }
}
