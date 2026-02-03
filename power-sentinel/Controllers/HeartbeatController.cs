using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PowerSentinel.Data;
using PowerSentinel.Models;
using PowerSentinel.Services;

namespace PowerSentinel.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HeartbeatController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly HeartbeatOptions _hbOpts;

    public HeartbeatController(AppDbContext db, IOptions<HeartbeatOptions> hbOpts)
    {
        _db = db;
        _hbOpts = hbOpts?.Value ?? new HeartbeatOptions();
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] HeartbeatDto dto)
    {
        // If a secret is configured, require the client to send it in the X-Heartbeat-Token header.
        if (!string.IsNullOrWhiteSpace(_hbOpts.HeartbeatToken))
        {
            if (!Request.Headers.TryGetValue("X-Heartbeat-Token", out var token) || string.IsNullOrEmpty(token))
            {
                return Unauthorized("missing heartbeat token");
            }

            if (!string.Equals(token.ToString(), _hbOpts.HeartbeatToken, StringComparison.Ordinal))
            {
                return Unauthorized("invalid heartbeat token");
            }
        }

        var now = DateTime.Now;

        if (dto == null || string.IsNullOrWhiteSpace(dto.DeviceId))
            return BadRequest("deviceId is required");

        var device = await _db.Devices.FindAsync(new object[] { dto.DeviceId }, HttpContext.RequestAborted);
        if (device == null)
        {
            device = new Device { Id = dto.DeviceId, Description = dto.Description, Heartbeat = now };
            _db.Devices.Add(device);
        }
        else
        {
            device.Heartbeat = now;
            if (!string.IsNullOrWhiteSpace(dto.Description)) device.Description = dto.Description;
            _db.Devices.Update(device);
        }

        await _db.SaveChangesAsync(HttpContext.RequestAborted);
        return Ok();
    }

    public class HeartbeatDto
    {
        public string? DeviceId { get; set; }
        public string? Description { get; set; }
    }
}
