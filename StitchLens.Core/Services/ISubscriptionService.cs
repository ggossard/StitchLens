using StitchLens.Data.Models;

namespace StitchLens.Core.Services;

public interface ISubscriptionService {
    // Create standard subscription
    Task<Subscription> CreateSubscriptionAsync(
        int userId,
        SubscriptionTier tier,
        string stripePriceId);

    // Create custom subscription
    Task<Subscription> CreateCustomSubscriptionAsync(
        int userId,
        decimal monthlyPrice,
        int downloadQuota,
        bool allowCommercialUse,
        string customTierName,
        string? customTierNotes = null);

    // Cancel subscription
    Task CancelSubscriptionAsync(int subscriptionId, string reason);

    // Upgrade/downgrade subscription
    Task<Subscription> UpgradeSubscriptionAsync(
        int currentSubscriptionId,
        SubscriptionTier newTier,
        string newStripePriceId);

    // Check if user can download
    Task<(bool CanDownload, string? Reason)> CanUserDownloadAsync(int userId);

    // Record a download
    Task RecordDownloadAsync(int userId, int projectId);

    // Get active subscription
    Task<Subscription?> GetActiveSubscriptionAsync(int userId);

    // Get subscription by ID
    Task<Subscription?> GetSubscriptionByIdAsync(int subscriptionId);

    // Get user's subscription history
    Task<List<Subscription>> GetUserSubscriptionsAsync(int userId);

    // Get payment history
    Task<List<PaymentHistory>> GetPaymentHistoryAsync(int userId);
}