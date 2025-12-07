namespace StitchLens.Data.Models;

public class PartnerConfig {
    public int Id { get; set; }
    public int UserId { get; set; }

    // Company Info
    public string CompanyName { get; set; } = string.Empty;
    public string WebsiteDomain { get; set; } = string.Empty;

    // Branding (for widget customization)
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }

    // API Access
    public string ApiKey { get; set; } = string.Empty;

    // Billing
    public string? StripeCustomerId { get; set; }
    public decimal MonthlyFee { get; set; } = 299m;
    public DateTime? SubscriptionStartDate { get; set; }
    public DateTime? SubscriptionEndDate { get; set; }
    public bool IsActive { get; set; } = true;

    // Usage Tracking
    public int PatternsGeneratedThisMonth { get; set; }
    public int PatternLimit { get; set; } = -1;  // -1 = unlimited
    public DateTime UsageResetDate { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}