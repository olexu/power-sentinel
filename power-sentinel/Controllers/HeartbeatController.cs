using Microsoft.AspNetCore.Mvc;
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
    public async Task<IActionResult> Post([FromBody] HeartbeatDto dto)
    {
        // If a secret is configured, require the client to send it in the Request-Token header.
        var requestToken = _configuration["RequestToken"];

        if (!string.IsNullOrWhiteSpace(requestToken))
        {
            if (!Request.Headers.TryGetValue("Request-Token", out var token) || string.IsNullOrEmpty(token))
            {
                return Unauthorized("Missing request token");
            }

            if (!string.Equals(token.ToString(), requestToken, StringComparison.Ordinal))
            {
                return Unauthorized("Invalid request token");
            }
        }

        var now = DateTime.Now;

        if (dto == null || string.IsNullOrWhiteSpace(dto.DeviceId))
            return BadRequest("DeviceId is required");

        var device = await _db.Devices.FindAsync(new object[] { dto.DeviceId }, HttpContext.RequestAborted);
        if (device == null)
        {
            device = new Device { Id = dto.DeviceId, Description = dto.Description ?? dto.DeviceId, Heartbeat = now };
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
