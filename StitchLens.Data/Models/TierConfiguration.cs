namespace StitchLens.Data.Models;

public class TierConfiguration {
    public int Id { get; set; }
    public SubscriptionTier Tier { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Quotas
    public int PatternCreationQuota { get; set; }
    public int PatternCreationDailyLimit { get; set; }

    // Features
    public bool AllowCommercialUse { get; set; }
    public bool PrioritySupport { get; set; }

    // Pricing (for reference)
    public decimal MonthlyPrice { get; set; }
    public decimal? AnnualPrice { get; set; }
    public decimal? PerPatternPrice { get; set; }

    // Stripe Price IDs (for checkout)
    public string? StripeMonthlyPriceId { get; set; }
    public string? StripeAnnualPriceId { get; set; }
    public string? StripePerPatternPriceId { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
