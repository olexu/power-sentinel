using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PowerSentinel.Models;

public class Subscriber
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("chat_id")]
    public long ChatId { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("device_id")]
    public string? DeviceId { get; set; }
}
