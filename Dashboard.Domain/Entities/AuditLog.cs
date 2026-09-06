public class AuditLog
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public string Details { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public AuditLog() { }

    public AuditLog(string eventType, string? userEmail, string details)
    {
        EventType = eventType;
        UserEmail = userEmail;
        Details = details;
        OccurredAt = DateTime.UtcNow;
    }
}