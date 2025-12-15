namespace StitchLens.Data.Models;

public class PaymentHistory {
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? SubscriptionId { get; set; }
    public int? ProjectId { get; set; }

    // Payment details
    public PaymentType Type { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public PaymentStatus Status { get; set; }

    // Description
    public string Description { get; set; } = string.Empty;

    // Stripe integration
    public string? StripePaymentIntentId { get; set; }
    public string? StripeInvoiceId { get; set; }

    // Dates
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public decimal? RefundAmount { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public Subscription? Subscription { get; set; }
    public Project? Project { get; set; }
}

public enum PaymentType {
    SubscriptionRecurring,
    SubscriptionInitial,
    OneTimePattern,
    Refund
}

public enum PaymentStatus {
    Pending,
    Succeeded,
    Failed,
    Refunded,
    PartiallyRefunded
}
