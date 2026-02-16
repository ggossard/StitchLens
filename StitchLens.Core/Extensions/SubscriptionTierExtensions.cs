using StitchLens.Data.Models;

namespace StitchLens.Core.Extensions;

public static class SubscriptionTierExtensions {
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
            SubscriptionTier.PayAsYouGo => "Pay per pattern and save your projects",
            SubscriptionTier.Hobbyist => "Monthly plan for regular stitchers",
            SubscriptionTier.Creator => "Monthly plan with commercial usage",
            SubscriptionTier.Custom => "Custom pricing and quota",
            _ => ""
        };
    }
}
