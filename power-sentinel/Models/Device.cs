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

    [Column("heartbeat_ttl_seconds")]
    public int HeartbeatTtlSeconds { get; set; } = 60;

    [Column("heartbeat_last_at")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? HeartbeatLastAt { get; set; }

    public ICollection<Event> Events { get; set; } = new List<Event>();
}
