// Dashboard.Domain/Entities/AuditLog.cs
namespace Dashboard.Domain.Entities;

public class AuditLog
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty;   // e.g. "ProductCreated", "UserLoggedIn"
    public string? UserEmail { get; set; }
    public string Details { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}