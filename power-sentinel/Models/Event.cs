using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PowerSentinel.Models;

public class Event
{
    [Key]
    [Column("id")]
    public long Id { get; set; }
    
    [Required]
    [Column("device_id")]
    public string DeviceId { get; set; } = string.Empty;
    
    [Column("is_power_on")]
    public bool IsPowerOn { get; set; }
    
    [Column("start_at")]
    public DateTime StartAt { get; set; }
    
    [Column("end_at")]
    public DateTime? EndAt { get; set; }

    public Device Device { get; set; } = null!;
}
