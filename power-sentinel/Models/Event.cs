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

    [Column("date")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
    public DateTime Date { get; set; }

    public Device Device { get; set; } = null!;
}
