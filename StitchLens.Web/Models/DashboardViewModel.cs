using StitchLens.Data.Models;
using StitchLens.Core.Extensions;

namespace StitchLens.Web.Models;

public class DashboardViewModel {
    public User User { get; set; } = null!;
    public SubscriptionTier CurrentTier { get; set; }
    public int PatternsCreatedThisMonth { get; set; }
    public int PatternCreationQuota { get; set; }
    public int PatternsCreatedToday { get; set; }
    public DateTime? NextBillingDate { get; set; }
    public decimal MonthlyPrice { get; set; }
    public BillingCycle? BillingCycle { get; set; }
    public bool HasActiveSubscription { get; set; }

    public List<Subscription> RecentSubscriptions { get; set; } = new();
    public List<PaymentHistory> RecentPayments { get; set; } = new();

    // Computed properties
    public int PatternsRemaining => Math.Max(0, PatternCreationQuota - PatternsCreatedThisMonth);
    public int PatternCreationPercentage => PatternCreationQuota > 0
        ? (int)((double)PatternsCreatedThisMonth / PatternCreationQuota * 100)
        : 0;
    public string TierDisplayName => CurrentTier.GetDisplayName();
    public string TierDescription => CurrentTier.GetDescription();
    public string PlanPriceLabel => BillingCycle == Data.Models.BillingCycle.Annual ? "Annual Price" : "Monthly Price";
    public string PlanPricePeriod => BillingCycle == Data.Models.BillingCycle.Annual ? "per year" : "per month";
}
