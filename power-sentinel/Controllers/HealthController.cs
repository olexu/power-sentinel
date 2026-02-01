using Microsoft.AspNetCore.Mvc;
using PowerSentinel.Data;

namespace PowerSentinel.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<HealthController> _logger;

    public HealthController(AppDbContext db, ILogger<HealthController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var now = DateTime.Now;
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(HttpContext.RequestAborted);
            if (canConnect)
            {
                return Ok(new { status = "Healthy", timestamp = now });
            }

            _logger.LogWarning("Health check: cannot connect to database");
            return StatusCode(503, new { status = "Unhealthy", reason = "Cannot connect to database", timestamp = now });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(503, new { status = "Unhealthy", reason = ex.Message, timestamp = now });
        }
    }
}
