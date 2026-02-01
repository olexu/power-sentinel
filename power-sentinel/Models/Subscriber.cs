using System.ComponentModel.DataAnnotations;

namespace PowerSentinel.Models;

public class Subscriber
{
    [Key]
    public int Id { get; set; }

    // Telegram chat id
    public long ChatId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    // Optional device id filter. Null = all devices
    public string? DeviceId { get; set; }
}
