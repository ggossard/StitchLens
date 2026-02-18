namespace StitchLens.Data.Models;

public class WebhookEventLog {
    public int Id { get; set; }
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public WebhookEventStatus Status { get; set; } = WebhookEventStatus.Processing;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string? LastError { get; set; }
}

public enum WebhookEventStatus {
    Processing,
    Processed,
    Failed
}
