using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using StitchLens.Core.Services;
using StitchLens.Data;
using StitchLens.Data.Models;
using System.Text.Json;

namespace StitchLens.Web.Controllers;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/[controller]")]
public class WebhookController : ControllerBase {
    private readonly StitchLensDbContext _context;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        StitchLensDbContext context,
        ISubscriptionService subscriptionService,
        IConfiguration configuration,
        ILogger<WebhookController> logger) {
        _context = context;
        _subscriptionService = subscriptionService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("stripe")]
    public async Task<IActionResult> HandleStripeWebhook() {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        WebhookEventLog? webhookEventLog = null;

        try {
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                _configuration["Stripe:WebhookSecret"]
            );

            _logger.LogInformation(
                "Stripe webhook received. EventId={EventId}, EventType={EventType}",
                stripeEvent.Id,
                stripeEvent.Type);

            webhookEventLog = await TryStartWebhookProcessingAsync(stripeEvent);
            if (webhookEventLog == null) {
                _logger.LogInformation(
                    "Skipping duplicate Stripe webhook event. EventId={EventId}, EventType={EventType}",
                    stripeEvent.Id,
                    stripeEvent.Type);
                return Ok();
            }

            switch (stripeEvent.Type) {
                case "checkout.session.completed":
                    await HandleCheckoutCompleted(stripeEvent, json);
                    break;

                case "customer.subscription.updated":
                    await HandleSubscriptionUpdated(stripeEvent, json);
                    break;

                case "customer.subscription.deleted":
                    await HandleSubscriptionDeleted(stripeEvent, json);
                    break;

                case "invoice.payment_succeeded":
                    await HandleInvoicePaymentSucceeded(stripeEvent, json);
                    break;

                case "invoice.payment_failed":
                    await HandleInvoicePaymentFailed(stripeEvent, json);
                    break;

                default:
                    _logger.LogInformation(
                        "Unhandled Stripe webhook event type. EventId={EventId}, EventType={EventType}",
                        stripeEvent.Id,
                        stripeEvent.Type);
                    break;
            }

            await MarkWebhookProcessedAsync(webhookEventLog.Id);

            return Ok();
        }
        catch (StripeException ex) {
            _logger.LogError(ex, "Stripe webhook signature verification failed");
            return BadRequest();
        }
        catch (Exception ex) {
            if (webhookEventLog != null) {
                await MarkWebhookFailedAsync(webhookEventLog.Id, ex.Message);
            }

            _logger.LogError(ex, "Error processing Stripe webhook");
            return StatusCode(500);
        }
    }

    private async Task<WebhookEventLog?> TryStartWebhookProcessingAsync(Event stripeEvent) {
        var existing = await _context.WebhookEventLogs
            .FirstOrDefaultAsync(e => e.EventId == stripeEvent.Id);

        if (existing != null) {
            if (existing.Status is WebhookEventStatus.Processed or WebhookEventStatus.Processing) {
                return null;
            }

            existing.Status = WebhookEventStatus.Processing;
            existing.LastError = null;
            existing.ReceivedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return existing;
        }

        var webhookEventLog = new WebhookEventLog {
            EventId = stripeEvent.Id,
            EventType = stripeEvent.Type,
            Status = WebhookEventStatus.Processing,
            ReceivedAt = DateTime.UtcNow
        };

        _context.WebhookEventLogs.Add(webhookEventLog);

        try {
            await _context.SaveChangesAsync();
            return webhookEventLog;
        }
        catch (DbUpdateException ex) when (IsDuplicateWebhookEventException(ex)) {
            return null;
        }
    }

    private async Task MarkWebhookProcessedAsync(int webhookEventLogId) {
        var webhookEventLog = await _context.WebhookEventLogs.FindAsync(webhookEventLogId);
        if (webhookEventLog == null) {
            return;
        }

        webhookEventLog.Status = WebhookEventStatus.Processed;
        webhookEventLog.ProcessedAt = DateTime.UtcNow;
        webhookEventLog.LastError = null;

        await _context.SaveChangesAsync();
    }

    private async Task MarkWebhookFailedAsync(int webhookEventLogId, string errorMessage) {
        var webhookEventLog = await _context.WebhookEventLogs.FindAsync(webhookEventLogId);
        if (webhookEventLog == null) {
            return;
        }

        webhookEventLog.Status = WebhookEventStatus.Failed;
        webhookEventLog.LastError = errorMessage.Length <= 1000
            ? errorMessage
            : errorMessage[..1000];

        await _context.SaveChangesAsync();
    }

    private static bool IsDuplicateWebhookEventException(DbUpdateException ex) {
        if (ex.InnerException is SqliteException sqliteEx) {
            return sqliteEx.SqliteErrorCode == 19;
        }

        return ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true;
    }

    private async Task HandleCheckoutCompleted(Event stripeEvent, string rawJson) {
        var session = stripeEvent.Data.Object as Session;
        if (session == null) return;

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

        // Get the subscription ID from the session
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

        // Fetch the subscription details from Stripe to get pricing info
        var subscriptionService = new Stripe.SubscriptionService();
        var stripeSubscription = await subscriptionService.GetAsync(stripeSubscriptionId);

        // Get price from subscription
        var priceId = stripeSubscription.Items.Data[0].Price.Id;
        var amount = stripeSubscription.Items.Data[0].Price.UnitAmount ?? 0;

        // Parse period dates from the subscription JSON
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

        // Create subscription record in our database
        var subscription = new StitchLens.Data.Models.Subscription {
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

        // Update user
        var user = await _context.Users.FindAsync(userId);
        if (user != null) {
            user.CurrentTier = tier;
            user.ActiveSubscriptionId = subscription.Id;
            user.StripeCustomerId = session.CustomerId;
        }

        // Record payment - get IDs from ExpandableField
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

    private async Task HandleSubscriptionUpdated(Event stripeEvent, string rawJson) {
 // Try to cast to Stripe.Subscription for status and id
 var stripeSubscription = stripeEvent.Data.Object as Stripe.Subscription;
 if (stripeSubscription == null) return;

 var subscription = await _context.Subscriptions
 .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscription.Id);

 if (subscription == null) {
 _logger.LogWarning("Subscription not found for Stripe ID. StripeSubscriptionId={StripeSubscriptionId}", stripeSubscription.Id);
 return;
 }

 // Update status
 subscription.Status = stripeSubscription.Status switch {
 "active" => SubscriptionStatus.Active,
 "past_due" => SubscriptionStatus.PastDue,
 "canceled" => SubscriptionStatus.Canceled,
 "incomplete" => SubscriptionStatus.Incomplete,
 "incomplete_expired" => SubscriptionStatus.IncompleteExpired,
 "trialing" => SubscriptionStatus.Trialing,
 _ => subscription.Status
 };

 // Parse current_period_start/current_period_end from raw JSON (avoid relying on Stripe.NET property names)
 try {
 using var doc = JsonDocument.Parse(rawJson);
 var obj = doc.RootElement.GetProperty("data").GetProperty("object");

 if (obj.TryGetProperty("current_period_start", out var cps) && cps.ValueKind == JsonValueKind.Number) {
 var unix = cps.GetInt64();
 subscription.CurrentPeriodStart = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
 }

 if (obj.TryGetProperty("current_period_end", out var cpe) && cpe.ValueKind == JsonValueKind.Number) {
 var unix = cpe.GetInt64();
 subscription.CurrentPeriodEnd = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
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

 private async Task HandleSubscriptionDeleted(Event stripeEvent, string rawJson) {
 var stripeSubscription = stripeEvent.Data.Object as Stripe.Subscription;
 if (stripeSubscription == null) return;

 var subscription = await _context.Subscriptions
 .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscription.Id);

 if (subscription == null) return;

 subscription.Status = SubscriptionStatus.Canceled;
 subscription.EndDate = DateTime.UtcNow;
 subscription.UpdatedAt = DateTime.UtcNow;

 // Update user
 var user = await _context.Users.FindAsync(subscription.UserId);
 if (user != null) {
 user.CurrentTier = SubscriptionTier.PayAsYouGo;
 user.ActiveSubscriptionId = null;
 }

 await _context.SaveChangesAsync();

  _logger.LogInformation("Subscription cancelled from webhook. SubscriptionId={SubscriptionId}", subscription.Id);
 }

    private async Task HandleInvoicePaymentSucceeded(Event stripeEvent, string rawJson) {
        var invoice = stripeEvent.Data.Object as Stripe.Invoice;
        if (invoice == null) {
            _logger.LogWarning("Invoice is null in payment_succeeded event");
            return;
        }

        _logger.LogInformation("Processing invoice payment. InvoiceId={InvoiceId}", invoice.Id);

        // Get subscription ID from nested parent.subscription_details.subscription
        string? subscriptionId = null;
        string? paymentIntentId = null;

        try {
            using var doc = JsonDocument.Parse(rawJson);
            var obj = doc.RootElement.GetProperty("data").GetProperty("object");

            // Navigate to parent.subscription_details.subscription
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

            // Fallback: Try to get from line items if not in parent
            if (string.IsNullOrEmpty(subscriptionId) && obj.TryGetProperty("lines", out var linesProp)) {
                _logger.LogDebug("Trying to extract subscription from invoice line items.");

                if (linesProp.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Array) {
                    var firstLine = dataProp.EnumerateArray().FirstOrDefault();
                    if (firstLine.ValueKind != JsonValueKind.Undefined) {
                        if (firstLine.TryGetProperty("parent", out var lineParentProp) &&
                            lineParentProp.TryGetProperty("subscription_item_details", out var sidProp) &&
                            sidProp.TryGetProperty("subscription", out var lineSubProp) &&
                            lineSubProp.ValueKind == JsonValueKind.String) {
                            subscriptionId = lineSubProp.GetString();
                            _logger.LogDebug("Found subscription ID in line items. StripeSubscriptionId={StripeSubscriptionId}", subscriptionId);
                        }
                    }
                }
            }

            if (obj.TryGetProperty("payment_intent", out var piProp) && piProp.ValueKind == JsonValueKind.String)
                paymentIntentId = piProp.GetString();
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error parsing invoice JSON");
        }

        if (string.IsNullOrEmpty(subscriptionId)) {
            _logger.LogWarning("No subscription ID found in invoice after checking all locations");
            return;
        }

        _logger.LogInformation("Processing renewal for subscription. StripeSubscriptionId={StripeSubscriptionId}", subscriptionId);

        // Find subscription in database
        var subscription = await _context.Subscriptions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscriptionId);

        if (subscription == null) {
            _logger.LogWarning("Subscription not found in database. StripeSubscriptionId={StripeSubscriptionId}", subscriptionId);
            return;
        }

        _logger.LogInformation("Found subscription in database. SubscriptionId={SubscriptionId}, UserId={UserId}", subscription.Id, subscription.UserId);

        // Record payment
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
            Type = PaymentType.SubscriptionRecurring,  // This is the correct enum value for renewals
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow
        };

        _context.PaymentHistory.Add(paymentHistory);
        _logger.LogInformation("Created recurring payment history record. Amount={Amount}", invoice.AmountPaid / 100m);

        // Reset monthly pattern creation usage for user
        var user = subscription.User;
        if (user != null) {
            user.PatternsCreatedThisMonth = 0;
            user.LastPatternCreationDate = DateTime.UtcNow;
            _logger.LogInformation("Reset pattern creation usage for user. UserId={UserId}", user.Id);
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Successfully processed invoice payment. StripeSubscriptionId={StripeSubscriptionId}", subscriptionId);
    }

    private async Task HandleInvoicePaymentFailed(Event stripeEvent, string rawJson) {
 var invoice = stripeEvent.Data.Object as Stripe.Invoice;
 if (invoice == null) return;

 string? subscriptionId = null;
 try {
 using var doc = JsonDocument.Parse(rawJson);
 var obj = doc.RootElement.GetProperty("data").GetProperty("object");

 if (obj.TryGetProperty("subscription", out var subProp) && subProp.ValueKind == JsonValueKind.String)
 subscriptionId = subProp.GetString();
 }
 catch (Exception ex) {
 _logger.LogWarning(ex, "Unable to parse invoice fields from webhook JSON");
 }

 if (string.IsNullOrEmpty(subscriptionId)) return;

 var subscription = await _context.Subscriptions
 .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscriptionId);

 if (subscription == null) return;

 subscription.Status = SubscriptionStatus.PastDue;
 subscription.UpdatedAt = DateTime.UtcNow;

  if (!string.IsNullOrWhiteSpace(invoice.Id) &&
      await _context.PaymentHistory.AnyAsync(p => p.StripeInvoiceId == invoice.Id && p.Status == PaymentStatus.Failed)) {
  _logger.LogInformation("Failed invoice already recorded. InvoiceId={InvoiceId}", invoice.Id);
  return;
  }

  // Record failed payment
  var payment = new PaymentHistory {
 UserId = subscription.UserId,
 SubscriptionId = subscription.Id,
 Type = PaymentType.SubscriptionRecurring,
 Amount = (invoice.AmountDue /100m),
 Currency = invoice.Currency?.ToUpper() ?? "USD",
 Status = PaymentStatus.Failed,
 Description = $"Failed payment for {subscription.Tier} subscription",
 StripeInvoiceId = invoice.Id,
 CreatedAt = DateTime.UtcNow
 };

 _context.PaymentHistory.Add(payment);
 await _context.SaveChangesAsync();

  _logger.LogWarning("Payment failed for subscription. SubscriptionId={SubscriptionId}, InvoiceId={InvoiceId}", subscription.Id, invoice.Id);

 // TODO: Send email notification to user
 }
}
