using Microsoft.EntityFrameworkCore;
using StitchLens.Data;
using StitchLens.Data.Models;
using Stripe;

namespace StitchLens.Core.Services;

public class SubscriptionService : ISubscriptionService {
    private readonly StitchLensDbContext _context;

    public SubscriptionService(StitchLensDbContext context) {
        _context = context;
    }

    public async Task<Data.Models.Subscription> CreateSubscriptionAsync(
        int userId,
        SubscriptionTier tier,
        string stripePriceId) {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            throw new ArgumentException("User not found", nameof(userId));

        // Cancel any existing active subscription
        var existingSubscription = await GetActiveSubscriptionAsync(userId);
        if (existingSubscription != null) {
            await CancelSubscriptionAsync(existingSubscription.Id, "Upgrading to new plan");
        }

        // Create Stripe customer if doesn't exist
        if (string.IsNullOrEmpty(user.StripeCustomerId)) {
            var customerService = new CustomerService();
            var customer = await customerService.CreateAsync(new CustomerCreateOptions {
                Email = user.Email,
                Metadata = new Dictionary<string, string>
                {
                    { "user_id", userId.ToString() }
                }
            });
            user.StripeCustomerId = customer.Id;
        }

        // Create Stripe subscription
        var subscriptionService = new Stripe.SubscriptionService();
        var stripeSubscription = await subscriptionService.CreateAsync(new SubscriptionCreateOptions {
            Customer = user.StripeCustomerId,
            Items = new List<SubscriptionItemOptions>
            {
                new SubscriptionItemOptions { Price = stripePriceId }
            },
            PaymentBehavior = "default_incomplete",
            PaymentSettings = new SubscriptionPaymentSettingsOptions {
                SaveDefaultPaymentMethod = "on_subscription"
            },
            Expand = new List<string> { "latest_invoice.payment_intent" }
        });

        // Create our subscription record
        var subscription = new Data.Models.Subscription {
            UserId = userId,
            Tier = tier,
            MonthlyPrice = tier.GetStandardPrice(),
            DownloadQuota = tier.GetStandardQuota(),
            AllowCommercialUse = tier.GetStandardCommercialRights(),
            Status = SubscriptionStatus.Incomplete,
            StartDate = DateTime.UtcNow,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
            NextBillingDate = DateTime.UtcNow.AddMonths(1),
            StripeSubscriptionId = stripeSubscription.Id,
            StripePriceId = stripePriceId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Subscriptions.Add(subscription);

        // Update user's current tier and active subscription
        user.CurrentTier = tier;
        user.ActiveSubscriptionId = subscription.Id;
        user.DownloadsThisMonth = 0;

        await _context.SaveChangesAsync();

        return subscription;
    }

    public async Task<Data.Models.Subscription> CreateCustomSubscriptionAsync(
        int userId,
        decimal monthlyPrice,
        int downloadQuota,
        bool allowCommercialUse,
        string customTierName,
        string? customTierNotes = null) {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            throw new ArgumentException("User not found", nameof(userId));

        // Cancel any existing active subscription
        var existingSubscription = await GetActiveSubscriptionAsync(userId);
        if (existingSubscription != null) {
            await CancelSubscriptionAsync(existingSubscription.Id, "Upgrading to custom plan");
        }

        var subscription = new Data.Models.Subscription {
            UserId = userId,
            Tier = SubscriptionTier.Custom,
            MonthlyPrice = monthlyPrice,
            DownloadQuota = downloadQuota,
            AllowCommercialUse = allowCommercialUse,
            CustomTierName = customTierName,
            CustomTierNotes = customTierNotes,
            Status = SubscriptionStatus.Active,
            StartDate = DateTime.UtcNow,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
            NextBillingDate = DateTime.UtcNow.AddMonths(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Subscriptions.Add(subscription);

        // Update user
        user.CurrentTier = SubscriptionTier.Custom;
        user.ActiveSubscriptionId = subscription.Id;
        user.DownloadsThisMonth = 0;

        await _context.SaveChangesAsync();

        return subscription;
    }

    public async Task CancelSubscriptionAsync(int subscriptionId, string reason) {
        var subscription = await _context.Subscriptions.FindAsync(subscriptionId);
        if (subscription == null)
            throw new ArgumentException("Subscription not found", nameof(subscriptionId));

        // Cancel in Stripe if exists
        if (!string.IsNullOrEmpty(subscription.StripeSubscriptionId)) {
            var service = new Stripe.SubscriptionService();
            await service.CancelAsync(subscription.StripeSubscriptionId);
        }

        subscription.Status = SubscriptionStatus.Canceled;
        subscription.CancelledAt = DateTime.UtcNow;
        subscription.CancellationReason = reason;
        subscription.EndDate = DateTime.UtcNow;
        subscription.UpdatedAt = DateTime.UtcNow;

        // Update user
        var user = await _context.Users.FindAsync(subscription.UserId);
        if (user != null) {
            user.CurrentTier = SubscriptionTier.Free;
            user.ActiveSubscriptionId = null;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<Data.Models.Subscription> UpgradeSubscriptionAsync(
        int currentSubscriptionId,
        SubscriptionTier newTier,
        string newStripePriceId) {
        var currentSubscription = await _context.Subscriptions.FindAsync(currentSubscriptionId);
        if (currentSubscription == null)
            throw new ArgumentException("Subscription not found", nameof(currentSubscriptionId));

        // Cancel current subscription
        await CancelSubscriptionAsync(currentSubscriptionId, "Upgrading to new tier");

        // Create new subscription
        return await CreateSubscriptionAsync(
            currentSubscription.UserId,
            newTier,
            newStripePriceId);
    }

    public async Task<(bool CanDownload, string? Reason)> CanUserDownloadAsync(int userId) {
        var user = await _context.Users
            .Include(u => u.ActiveSubscription)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return (false, "User not found");

        // Check daily limit (20 patterns per day for abuse prevention)
        if (user.PatternsCreatedToday >= 20 &&
            user.LastPatternDate.Date == DateTime.UtcNow.Date) {
            return (false, "Daily limit of 20 patterns reached. Please try again tomorrow.");
        }

        // Free tier - always needs to pay per pattern
        if (user.CurrentTier == SubscriptionTier.Free) {
            return (true, null); // Will be charged per download
        }

        // Reset monthly counter if new month
        if (user.LastDownloadDate.Month != DateTime.UtcNow.Month ||
            user.LastDownloadDate.Year != DateTime.UtcNow.Year) {
            user.DownloadsThisMonth = 0;
            await _context.SaveChangesAsync();
        }

        // Check subscription quota
        if (user.ActiveSubscription == null) {
            return (false, "No active subscription");
        }

        if (user.ActiveSubscription.Status != SubscriptionStatus.Active) {
            return (false, "Subscription is not active");
        }

        if (user.DownloadsThisMonth >= user.ActiveSubscription.DownloadQuota) {
            return (false, $"Monthly quota of {user.ActiveSubscription.DownloadQuota} downloads reached. Resets on {DateTime.UtcNow.AddMonths(1):MMMM 1}.");
        }

        return (true, null);
    }

    public async Task RecordDownloadAsync(int userId, int projectId) {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            throw new ArgumentException("User not found", nameof(userId));

        // Update monthly download counter
        user.DownloadsThisMonth++;
        user.LastDownloadDate = DateTime.UtcNow;

        // Update daily pattern counter
        if (user.LastPatternDate.Date != DateTime.UtcNow.Date) {
            user.PatternsCreatedToday = 1;
        }
        else {
            user.PatternsCreatedToday++;
        }
        user.LastPatternDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task<Data.Models.Subscription?> GetActiveSubscriptionAsync(int userId) {
        return await _context.Subscriptions
            .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<Data.Models.Subscription?> GetSubscriptionByIdAsync(int subscriptionId) {
        return await _context.Subscriptions
            .Include(s => s.User)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId);
    }

    public async Task<List<Data.Models.Subscription>> GetUserSubscriptionsAsync(int userId) {
        return await _context.Subscriptions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<PaymentHistory>> GetPaymentHistoryAsync(int userId) {
        return await _context.PaymentHistory
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }
}