using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using StitchLens.Data;
using StitchLens.Data.Models;
using System.Text.Json;
using DataSubscription = StitchLens.Data.Models.Subscription;

namespace StitchLens.Web.Services;

public class StripeWebhookProcessor : IStripeWebhookProcessor {
    private readonly StitchLensDbContext _context;
    private readonly ILogger<StripeWebhookProcessor> _logger;

    public StripeWebhookProcessor(StitchLensDbContext context, ILogger<StripeWebhookProcessor> logger) {
        _context = context;
        _logger = logger;
    }

    public async Task HandleCheckoutCompletedAsync(Event stripeEvent, string rawJson) {
        var session = stripeEvent.Data.Object as Session;
        if (session == null) {
            return;
        }

        if (session.Metadata != null &&
            session.Metadata.TryGetValue("purchase_type", out var purchaseType) &&
            string.Equals(purchaseType, "one_time_pattern", StringComparison.OrdinalIgnoreCase)) {
            _logger.LogInformation("One-time pattern checkout completed. SessionId={SessionId}", session.Id);
            return;
        }

        if (session.Metadata == null ||
            !session.Metadata.ContainsKey("user_id") ||
            !session.Metadata.ContainsKey("tier")) {
            _logger.LogWarning("Checkout session missing subscription metadata. SessionId={SessionId}", session.Id);
            return;
        }

        if (!int.TryParse(session.Metadata["user_id"], out var userId)) {
            _logger.LogWarning(
                "Checkout session has invalid user_id metadata. SessionId={SessionId}, UserIdMetadata={UserIdMetadata}",
                session.Id,
                session.Metadata["user_id"]);
            return;
        }

        var tierName = session.Metadata["tier"];
        if (!Enum.TryParse<SubscriptionTier>(tierName, ignoreCase: true, out var tier)) {
            _logger.LogWarning(
                "Checkout session has invalid tier metadata. SessionId={SessionId}, TierMetadata={TierMetadata}",
                session.Id,
                tierName);
            return;
        }

        var billingCycle = BillingCycle.Monthly;
        if (session.Metadata.TryGetValue("billing_cycle", out var billingCycleValue) &&
            Enum.TryParse<BillingCycle>(billingCycleValue, ignoreCase: true, out var parsedBillingCycle)) {
            billingCycle = parsedBillingCycle;
        }

        _logger.LogInformation(
            "Processing checkout completion. UserId={UserId}, Tier={Tier}, BillingCycle={BillingCycle}",
            userId,
            tier,
            billingCycle);

        var stripeSubscriptionId = session.SubscriptionId;
        if (string.IsNullOrEmpty(stripeSubscriptionId)) {
            _logger.LogWarning("No subscription ID in checkout session. SessionId={SessionId}", session.Id);
            return;
        }

        var existingSubscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscriptionId);
        if (existingSubscription != null) {
            _logger.LogInformation(
                "Subscription already exists for checkout completion. SubscriptionId={SubscriptionId}, StripeSubscriptionId={StripeSubscriptionId}",
                existingSubscription.Id,
                stripeSubscriptionId);
            return;
        }

        var subscriptionService = new Stripe.SubscriptionService();
        var stripeSubscription = await subscriptionService.GetAsync(stripeSubscriptionId);

        var priceId = stripeSubscription.Items.Data[0].Price.Id;
        var amount = stripeSubscription.Items.Data[0].Price.UnitAmount ?? 0;

        DateTime? currentPeriodStart = null;
        DateTime? currentPeriodEnd = null;

        try {
            var subJson = stripeSubscription.StripeResponse.Content;
            using var doc = JsonDocument.Parse(subJson);
            var obj = doc.RootElement;

            if (obj.TryGetProperty("current_period_start", out var cps) && cps.ValueKind == JsonValueKind.Number) {
                currentPeriodStart = DateTimeOffset.FromUnixTimeSeconds(cps.GetInt64()).UtcDateTime;
            }

            if (obj.TryGetProperty("current_period_end", out var cpe) && cpe.ValueKind == JsonValueKind.Number) {
                currentPeriodEnd = DateTimeOffset.FromUnixTimeSeconds(cpe.GetInt64()).UtcDateTime;
            }
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Unable to parse subscription period dates");
        }

        var tierConfig = await _context.TierConfigurations
            .FirstOrDefaultAsync(t => t.Tier == tier);

        if (tierConfig == null) {
            _logger.LogError("Tier configuration not found. Tier={Tier}", tier);
            return;
        }

        var subscription = new DataSubscription {
            UserId = userId,
            Tier = tier,
            BillingCycle = billingCycle,
            Status = SubscriptionStatus.Active,
            StripeSubscriptionId = stripeSubscriptionId,
            StripePriceId = priceId,
            MonthlyPrice = amount / 100m,
            PatternCreationQuota = tierConfig.PatternCreationQuota,
            AllowCommercialUse = tierConfig.AllowCommercialUse,
            StartDate = DateTime.UtcNow,
            CurrentPeriodStart = currentPeriodStart ?? DateTime.UtcNow,
            CurrentPeriodEnd = currentPeriodEnd ?? (billingCycle == BillingCycle.Annual ? DateTime.UtcNow.AddYears(1) : DateTime.UtcNow.AddMonths(1)),
            NextBillingDate = currentPeriodEnd ?? (billingCycle == BillingCycle.Annual ? DateTime.UtcNow.AddYears(1) : DateTime.UtcNow.AddMonths(1)),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Subscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        var user = await _context.Users.FindAsync(userId);
        if (user != null) {
            user.CurrentTier = tier;
            user.ActiveSubscriptionId = subscription.Id;
            user.StripeCustomerId = session.CustomerId;
        }

        var paymentIntentId = session.PaymentIntent?.Id;
        var invoiceId = session.Invoice?.Id;

        var payment = new PaymentHistory {
            UserId = userId,
            SubscriptionId = subscription.Id,
            Type = PaymentType.SubscriptionInitial,
            Amount = (session.AmountTotal ?? 0) / 100m,
            Currency = session.Currency?.ToUpper() ?? "USD",
            Status = PaymentStatus.Succeeded,
            Description = $"Initial payment for {tier} subscription",
            StripePaymentIntentId = paymentIntentId,
            StripeInvoiceId = invoiceId,
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow
        };

        _context.PaymentHistory.Add(payment);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Subscription created from checkout completion. SubscriptionId={SubscriptionId}, UserId={UserId}, StripeSubscriptionId={StripeSubscriptionId}",
            subscription.Id,
            userId,
            stripeSubscriptionId);
    }

    public async Task HandleSubscriptionUpdatedAsync(Event stripeEvent, string rawJson) {
        var stripeSubscription = stripeEvent.Data.Object as Stripe.Subscription;
        if (stripeSubscription == null) {
            return;
        }

        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscription.Id);

        if (subscription == null) {
            _logger.LogWarning("Subscription not found for Stripe ID. StripeSubscriptionId={StripeSubscriptionId}", stripeSubscription.Id);
            return;
        }

        subscription.Status = stripeSubscription.Status switch {
            "active" => SubscriptionStatus.Active,
            "past_due" => SubscriptionStatus.PastDue,
            "canceled" => SubscriptionStatus.Canceled,
            "incomplete" => SubscriptionStatus.Incomplete,
            "incomplete_expired" => SubscriptionStatus.IncompleteExpired,
            "trialing" => SubscriptionStatus.Trialing,
            _ => subscription.Status
        };

        try {
            using var doc = JsonDocument.Parse(rawJson);
            var obj = doc.RootElement.GetProperty("data").GetProperty("object");

            if (obj.TryGetProperty("current_period_start", out var cps) && cps.ValueKind == JsonValueKind.Number) {
                subscription.CurrentPeriodStart = DateTimeOffset.FromUnixTimeSeconds(cps.GetInt64()).UtcDateTime;
            }

            if (obj.TryGetProperty("current_period_end", out var cpe) && cpe.ValueKind == JsonValueKind.Number) {
                subscription.CurrentPeriodEnd = DateTimeOffset.FromUnixTimeSeconds(cpe.GetInt64()).UtcDateTime;
            }
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Unable to parse current_period_start/end from webhook JSON");
        }

        subscription.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Subscription updated from webhook. SubscriptionId={SubscriptionId}, Status={Status}",
            subscription.Id,
            subscription.Status);
    }

    public async Task HandleSubscriptionDeletedAsync(Event stripeEvent, string rawJson) {
        var stripeSubscription = stripeEvent.Data.Object as Stripe.Subscription;
        if (stripeSubscription == null) {
            return;
        }

        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscription.Id);

        if (subscription == null) {
            return;
        }

        subscription.Status = SubscriptionStatus.Canceled;
        subscription.EndDate = DateTime.UtcNow;
        subscription.UpdatedAt = DateTime.UtcNow;

        var user = await _context.Users.FindAsync(subscription.UserId);
        if (user != null) {
            user.CurrentTier = SubscriptionTier.PayAsYouGo;
            user.ActiveSubscriptionId = null;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Subscription cancelled from webhook. SubscriptionId={SubscriptionId}", subscription.Id);
    }

    public async Task HandleInvoicePaymentSucceededAsync(Event stripeEvent, string rawJson) {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice == null) {
            _logger.LogWarning("Invoice is null in payment_succeeded event");
            return;
        }

        _logger.LogInformation("Processing invoice payment. InvoiceId={InvoiceId}", invoice.Id);

        string? subscriptionId = null;
        string? paymentIntentId = null;

        try {
            using var doc = JsonDocument.Parse(rawJson);
            var obj = doc.RootElement.GetProperty("data").GetProperty("object");

            if (obj.TryGetProperty("parent", out var parentProp)) {
                _logger.LogDebug("Found invoice parent property.");

                if (parentProp.TryGetProperty("subscription_details", out var subDetailsProp)) {
                    _logger.LogDebug("Found invoice subscription_details property.");

                    if (subDetailsProp.TryGetProperty("subscription", out var subProp) &&
                        subProp.ValueKind == JsonValueKind.String) {
                        subscriptionId = subProp.GetString();
                        _logger.LogDebug("Found subscription ID in parent. StripeSubscriptionId={StripeSubscriptionId}", subscriptionId);
                    }
                }
            }

            if (string.IsNullOrEmpty(subscriptionId) && obj.TryGetProperty("lines", out var linesProp)) {
                _logger.LogDebug("Trying to extract subscription from invoice line items.");

                if (linesProp.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Array) {
                    var firstLine = dataProp.EnumerateArray().FirstOrDefault();
                    if (firstLine.ValueKind != JsonValueKind.Undefined &&
                        firstLine.TryGetProperty("parent", out var lineParentProp) &&
                        lineParentProp.TryGetProperty("subscription_item_details", out var sidProp) &&
                        sidProp.TryGetProperty("subscription", out var lineSubProp) &&
                        lineSubProp.ValueKind == JsonValueKind.String) {
                        subscriptionId = lineSubProp.GetString();
                        _logger.LogDebug("Found subscription ID in line items. StripeSubscriptionId={StripeSubscriptionId}", subscriptionId);
                    }
                }
            }

            if (obj.TryGetProperty("payment_intent", out var piProp) && piProp.ValueKind == JsonValueKind.String) {
                paymentIntentId = piProp.GetString();
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error parsing invoice JSON");
        }

        if (string.IsNullOrEmpty(subscriptionId)) {
            _logger.LogWarning("No subscription ID found in invoice after checking all locations");
            return;
        }

        _logger.LogInformation("Processing renewal for subscription. StripeSubscriptionId={StripeSubscriptionId}", subscriptionId);

        var subscription = await _context.Subscriptions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscriptionId);

        if (subscription == null) {
            _logger.LogWarning("Subscription not found in database. StripeSubscriptionId={StripeSubscriptionId}", subscriptionId);
            return;
        }

        _logger.LogInformation("Found subscription in database. SubscriptionId={SubscriptionId}, UserId={UserId}", subscription.Id, subscription.UserId);

        if (!string.IsNullOrWhiteSpace(invoice.Id) &&
            await _context.PaymentHistory.AnyAsync(p => p.StripeInvoiceId == invoice.Id && p.Status == PaymentStatus.Succeeded)) {
            _logger.LogInformation("Recurring invoice already processed. InvoiceId={InvoiceId}", invoice.Id);
            return;
        }

        var paymentHistory = new PaymentHistory {
            UserId = subscription.UserId,
            SubscriptionId = subscription.Id,
            Amount = invoice.AmountPaid / 100m,
            Currency = invoice.Currency?.ToUpper() ?? "USD",
            Status = PaymentStatus.Succeeded,
            Description = "Recurring payment for subscription",
            StripePaymentIntentId = paymentIntentId,
            StripeInvoiceId = invoice.Id,
            Type = PaymentType.SubscriptionRecurring,
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow
        };

        _context.PaymentHistory.Add(paymentHistory);
        _logger.LogInformation("Created recurring payment history record. Amount={Amount}", invoice.AmountPaid / 100m);

        var user = subscription.User;
        if (user != null) {
            user.PatternsCreatedThisMonth = 0;
            user.LastPatternCreationDate = DateTime.UtcNow;
            _logger.LogInformation("Reset pattern creation usage for user. UserId={UserId}", user.Id);
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Successfully processed invoice payment. StripeSubscriptionId={StripeSubscriptionId}", subscriptionId);
    }

    public async Task HandleInvoicePaymentFailedAsync(Event stripeEvent, string rawJson) {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice == null) {
            return;
        }

        string? subscriptionId = null;
        try {
            using var doc = JsonDocument.Parse(rawJson);
            var obj = doc.RootElement.GetProperty("data").GetProperty("object");

            if (obj.TryGetProperty("subscription", out var subProp) && subProp.ValueKind == JsonValueKind.String) {
                subscriptionId = subProp.GetString();
            }
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Unable to parse invoice fields from webhook JSON");
        }

        if (string.IsNullOrEmpty(subscriptionId)) {
            return;
        }

        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscriptionId);

        if (subscription == null) {
            return;
        }

        subscription.Status = SubscriptionStatus.PastDue;
        subscription.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(invoice.Id) &&
            await _context.PaymentHistory.AnyAsync(p => p.StripeInvoiceId == invoice.Id && p.Status == PaymentStatus.Failed)) {
            _logger.LogInformation("Failed invoice already recorded. InvoiceId={InvoiceId}", invoice.Id);
            return;
        }

        var payment = new PaymentHistory {
            UserId = subscription.UserId,
            SubscriptionId = subscription.Id,
            Type = PaymentType.SubscriptionRecurring,
            Amount = invoice.AmountDue / 100m,
            Currency = invoice.Currency?.ToUpper() ?? "USD",
            Status = PaymentStatus.Failed,
            Description = $"Failed payment for {subscription.Tier} subscription",
            StripeInvoiceId = invoice.Id,
            CreatedAt = DateTime.UtcNow
        };

        _context.PaymentHistory.Add(payment);
        await _context.SaveChangesAsync();

        _logger.LogWarning("Payment failed for subscription. SubscriptionId={SubscriptionId}, InvoiceId={InvoiceId}", subscription.Id, invoice.Id);
    }
}
