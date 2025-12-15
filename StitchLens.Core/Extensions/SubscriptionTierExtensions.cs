using StitchLens.Data.Models;

namespace StitchLens.Core.Extensions;

public static class SubscriptionTierExtensions {
    public static decimal GetStandardPrice(this SubscriptionTier tier) {
        return tier switch {
            SubscriptionTier.Free => 0m,
            SubscriptionTier.Hobbyist => 12.99m,
            SubscriptionTier.Creator => 49.99m,
            SubscriptionTier.Custom => throw new InvalidOperationException(
                "Custom tier pricing must be set manually"),
            _ => throw new ArgumentException($"Unknown tier: {tier}")
        };
    }

    public static int GetStandardQuota(this SubscriptionTier tier) {
        return tier switch {
            SubscriptionTier.Free => 0,
            SubscriptionTier.Hobbyist => 10,
            SubscriptionTier.Creator => 100,
            SubscriptionTier.Custom => throw new InvalidOperationException(
                "Custom tier quota must be set manually"),
            _ => throw new ArgumentException($"Unknown tier: {tier}")
        };
    }

    public static bool GetStandardCommercialRights(this SubscriptionTier tier) {
        return tier switch {
            SubscriptionTier.Free => false,
            SubscriptionTier.Hobbyist => false,
            SubscriptionTier.Creator => true,
            SubscriptionTier.Custom => throw new InvalidOperationException(
                "Custom tier commercial rights must be set manually"),
            _ => throw new ArgumentException($"Unknown tier: {tier}")
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
            SubscriptionTier.Free => "Save patterns and get 20% off downloads",
            SubscriptionTier.Hobbyist => "10 pattern downloads per month for personal use",
            SubscriptionTier.Creator => "100 pattern downloads per month with commercial license",
            SubscriptionTier.Custom => "Custom pricing and quota",
            _ => ""
        };
    }
}