using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace StitchLens.Data.Models;

public class User : IdentityUser<int>  // <int> means Id is int, not string
{
    // ASP.NET Identity provides: Id, Email, PasswordHash, EmailConfirmed, etc.

    // Your custom fields
    public UserType UserType { get; set; } = UserType.Customer;
    public string PlanType { get; set; } = "PayAsYouGo"; // PayAsYouGo, Premium, B2B_Basic, B2B_Pro
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [StringLength(50)]
    public string? Nickname { get; set; }

    // Current subscription status (for quick queries)
    public SubscriptionTier CurrentTier { get; set; } = SubscriptionTier.PayAsYouGo;
    public int? ActiveSubscriptionId { get; set; }

    // Usage tracking (reset monthly)
    public int PatternsCreatedThisMonth { get; set; }
    public DateTime LastPatternCreationDate { get; set; } = DateTime.UtcNow;
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
    PayAsYouGo = 0,
    Hobbyist = 1,
    Creator = 2,
    Custom = 99
}
