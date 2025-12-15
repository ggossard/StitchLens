using StitchLens.Data.Models;

namespace StitchLens.Web.Models;

public class PricingViewModel {
    public bool IsAuthenticated { get; set; }
    public SubscriptionTier? CurrentTier { get; set; }
    public List<PricingTier> Tiers { get; set; } = new();
}

public class PricingTier {
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public string PriceDisplay { get; set; } = string.Empty;
    public SubscriptionTier Tier { get; set; }
    public bool IsPopular { get; set; }
    public List<string> Features { get; set; } = new();
    public string ButtonText { get; set; } = "Get Started";
    public string ButtonClass { get; set; } = "btn-primary";
    public bool IsCurrent { get; set; }
}