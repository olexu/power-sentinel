using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PowerSentinel.Data;
using PowerSentinel.Models;

namespace PowerSentinel.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HeartbeatController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public HeartbeatController(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromQuery] string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return BadRequest("DeviceId is required");

        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, HttpContext.RequestAborted);
        
        if (device == null)
        {
            return NotFound($"Unknown device '{deviceId}'");
        }
        if (!string.IsNullOrWhiteSpace(device.HeartbeatKey))
        {
            if (!Request.Headers.TryGetValue("Heartbeat-Key", out var hbKey) || string.IsNullOrWhiteSpace(hbKey))
            {
                return Unauthorized("Missing device heartbeat key");
            }
            if (!string.Equals(hbKey.ToString(), device.HeartbeatKey, StringComparison.Ordinal))
            {
                return Unauthorized("Invalid device heartbeat key");
            }
        }

        device.Heartbeat = DateTime.Now;
        await _db.SaveChangesAsync(HttpContext.RequestAborted);
        return Ok();
    }
}
