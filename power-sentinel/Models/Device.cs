using System.ComponentModel.DataAnnotations;

namespace PowerSentinel.Models;

public class Device
{
    [Key]
    public string Id { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime? Heartbeat { get; set; }
}
