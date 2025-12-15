using StitchLens.Data.Models;

namespace StitchLens.Core.Services;

public static class SubscriptionTierExtensions {
    public static decimal GetStandardPrice(this SubscriptionTier tier) {
        return tier switch {
            SubscriptionTier.Free => 0m,
            SubscriptionTier.Hobbyist => 12.99m,
            SubscriptionTier.Creator => 49.99m,
            SubscriptionTier.Custom => 0m, // Custom pricing is set per subscription
            _ => 0m
        };
    }

    public static int GetStandardQuota(this SubscriptionTier tier) {
        return tier switch {
            SubscriptionTier.Free => 0, // Pay per pattern
            SubscriptionTier.Hobbyist => 10,
            SubscriptionTier.Creator => 100,
            SubscriptionTier.Custom => 0, // Custom quota is set per subscription
            _ => 0
        };
    }

    public static bool GetStandardCommercialRights(this SubscriptionTier tier) {
        return tier switch {
            SubscriptionTier.Free => false,
            SubscriptionTier.Hobbyist => false,
            SubscriptionTier.Creator => true,
            SubscriptionTier.Custom => false, // Custom rights set per subscription
            _ => false
        };
    }

    public static string GetDisplayName(this SubscriptionTier tier) {
        return tier switch {
            SubscriptionTier.Free => "Free Account",
            SubscriptionTier.Hobbyist => "Hobbyist",
            SubscriptionTier.Creator => "Creator",
            SubscriptionTier.Custom => "Custom Plan",
            _ => "Unknown"
        };
    }

    public static string GetDescription(this SubscriptionTier tier) {
        return tier switch {
            SubscriptionTier.Free => "Pay per pattern - $11.99 each",
            SubscriptionTier.Hobbyist => "10 downloads/month - Personal use only",
            SubscriptionTier.Creator => "100 downloads/month - Commercial license included",
            SubscriptionTier.Custom => "Custom quota and pricing",
            _ => ""
        };
    }
}