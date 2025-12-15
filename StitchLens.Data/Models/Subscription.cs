namespace StitchLens.Data.Models;

public class Subscription {
    public int Id { get; set; }
    public int UserId { get; set; }

    // Plan details
    public SubscriptionTier Tier { get; set; }
    public decimal MonthlyPrice { get; set; }
    public int DownloadQuota { get; set; }
    public bool AllowCommercialUse { get; set; }

    // Custom tier metadata
    public string? CustomTierName { get; set; }
    public string? CustomTierNotes { get; set; }

    // Lifecycle
    public SubscriptionStatus Status { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    // Billing
    public DateTime CurrentPeriodStart { get; set; }
    public DateTime CurrentPeriodEnd { get; set; }
    public DateTime? NextBillingDate { get; set; }

    // Stripe integration
    public string? StripeSubscriptionId { get; set; }
    public string? StripePriceId { get; set; }
    public string? StripePaymentMethodId { get; set; }

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public ICollection<PaymentHistory> Payments { get; set; } = new List<PaymentHistory>();
}

public enum SubscriptionStatus {
    Active,
    PastDue,
    Canceled,
    Expired,
    Trialing,
    Incomplete,
    IncompleteExpired
}
