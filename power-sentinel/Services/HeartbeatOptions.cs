namespace PowerSentinel.Services;

public class HeartbeatOptions
{
    // If set, heartbeat requests must include this value in the X-Heartbeat-Token header
    public string? Secret { get; set; }
}
