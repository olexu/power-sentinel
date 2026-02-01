using System.ComponentModel.DataAnnotations;

namespace PowerSentinel.Models;

public class Event
{
    [Key]
    public long Id { get; set; }
    public bool IsPowerOn { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }

    public TimeSpan? Duration => EndAt.HasValue ? EndAt - StartAt : null;
    public string? DeviceId { get; set; }
}
