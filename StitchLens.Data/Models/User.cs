using Microsoft.AspNetCore.Identity;

namespace StitchLens.Data.Models;

public class User : IdentityUser<int>  // <int> means Id is int, not string
{
    // ASP.NET Identity provides: Id, Email, PasswordHash, EmailConfirmed, etc.

    // Your custom fields
    public UserType UserType { get; set; } = UserType.Customer;
    public string PlanType { get; set; } = "Free"; // Free, Premium, B2B_Basic, B2B_Pro
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Current subscription status (for quick queries)
    public SubscriptionTier CurrentTier { get; set; } = SubscriptionTier.Free;
    public int? ActiveSubscriptionId { get; set; }

    // Usage tracking (reset monthly)
    public int DownloadsThisMonth { get; set; }
    public DateTime LastDownloadDate { get; set; } = DateTime.UtcNow;
    public int PatternsCreatedToday { get; set; }
    public DateTime LastPatternDate { get; set; } = DateTime.UtcNow;

    // Stripe customer reference (persistent across subscriptions)
    public string? StripeCustomerId { get; set; }

    // Navigation properties
    public ICollection<Project> Projects { get; set; } = new List<Project>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public Subscription? ActiveSubscription { get; set; }

    // For B2B partners (null for regular customers)
    public PartnerConfig? PartnerConfig { get; set; }
}

public enum SubscriptionTier {
    Free = 0,
    Hobbyist = 1,
    Creator = 2,
    Custom = 99
}
