using StitchLens.Data.Models;

namespace StitchLens.Core.Services;

public static class SubscriptionTierExtensions {
    public static decimal GetStandardPrice(this SubscriptionTier tier) {
        return tier switch {
            SubscriptionTier.PayAsYouGo => 5.95m,
            SubscriptionTier.Hobbyist => 12.95m,
            SubscriptionTier.Creator => 35.95m,
            SubscriptionTier.Custom => 0m, // Custom pricing is set per subscription
            _ => 0m
        };
    }

    public static int GetStandardPatternCreationQuota(this SubscriptionTier tier) {
        return tier switch {
            SubscriptionTier.PayAsYouGo => 0, // Pay per pattern
            SubscriptionTier.Hobbyist => 10,
            SubscriptionTier.Creator => 100,
            SubscriptionTier.Custom => 0, // Custom quota is set per subscription
            _ => 0
        };
    }

    public static bool GetStandardCommercialRights(this SubscriptionTier tier) {
        return tier switch {
            SubscriptionTier.PayAsYouGo => false,
            SubscriptionTier.Hobbyist => false,
            SubscriptionTier.Creator => true,
            SubscriptionTier.Custom => false, // Custom rights set per subscription
            _ => false
        };
    }

    public static string GetDisplayName(this SubscriptionTier tier) {
        return tier switch {
            SubscriptionTier.PayAsYouGo => "Pay As You Go",
            SubscriptionTier.Hobbyist => "Hobbyist",
            SubscriptionTier.Creator => "Creator",
            SubscriptionTier.Custom => "Custom Plan",
            _ => "Unknown"
        };
    }

    public static string GetDescription(this SubscriptionTier tier) {
        return tier switch {
            SubscriptionTier.PayAsYouGo => "Pay per pattern - $5.95 each",
            SubscriptionTier.Hobbyist => "10 patterns/month - Personal use only",
            SubscriptionTier.Creator => "100 patterns/month - Commercial license included",
            SubscriptionTier.Custom => "Custom quota and pricing",
            _ => ""
        };
    }
}
