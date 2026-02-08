using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PowerSentinel.Models;

public class Device
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("heartbeat_key")]
    [StringLength(128)]
    public string? HeartbeatKey { get; set; }

    [Column("heartbeat_at")]
    public DateTime? Heartbeat { get; set; }

    public ICollection<Event> Events { get; set; } = new List<Event>();
}
