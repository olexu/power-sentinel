using System.ComponentModel.DataAnnotations;

namespace PowerSentinel.Models;

public class Event
{
    [Key]
    public long Id { get; set; }
    [Required]
    public string DeviceId { get; set; } = string.Empty;
    public bool IsPowerOn { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }
}
