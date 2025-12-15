using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using StitchLens.Core.Services;
using StitchLens.Data;
using StitchLens.Data.Models;
using System.Text.Json;

namespace StitchLens.Web.Controllers;

[ApiController]
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

 try {
 var stripeEvent = EventUtility.ConstructEvent(
 json,
 Request.Headers["Stripe-Signature"],
 _configuration["Stripe:WebhookSecret"]
 );

 _logger.LogInformation($"Stripe webhook received: {stripeEvent.Type}");

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
 _logger.LogInformation($"Unhandled event type: {stripeEvent.Type}");
 break;
 }

 return Ok();
 }
 catch (StripeException ex) {
 _logger.LogError(ex, "Stripe webhook signature verification failed");
 return BadRequest();
 }
 catch (Exception ex) {
 _logger.LogError(ex, "Error processing Stripe webhook");
 return StatusCode(500);
 }
 }

    private async Task HandleCheckoutCompleted(Event stripeEvent, string rawJson) {
        var session = stripeEvent.Data.Object as Session;
        if (session == null) return;

        var userId = int.Parse(session.Metadata["user_id"]);
        var tierName = session.Metadata["tier"];
        var tier = Enum.Parse<SubscriptionTier>(tierName);

        _logger.LogInformation($"Processing checkout completion for user {userId}, tier {tier}");

        // Get the subscription ID from the session
        var stripeSubscriptionId = session.SubscriptionId;
        if (string.IsNullOrEmpty(stripeSubscriptionId)) {
            _logger.LogWarning("No subscription ID in checkout session");
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

        // Create subscription record in our database
        var subscription = new StitchLens.Data.Models.Subscription {
            UserId = userId,
            Tier = tier,
            Status = SubscriptionStatus.Active,
            StripeSubscriptionId = stripeSubscriptionId,
            StripePriceId = priceId,
            MonthlyPrice = amount / 100m,
            DownloadQuota = tier.GetStandardQuota(),
            AllowCommercialUse = tier.GetStandardCommercialRights(),
            StartDate = DateTime.UtcNow,
            CurrentPeriodStart = currentPeriodStart ?? DateTime.UtcNow,
            CurrentPeriodEnd = currentPeriodEnd ?? DateTime.UtcNow.AddMonths(1),
            NextBillingDate = currentPeriodEnd ?? DateTime.UtcNow.AddMonths(1),
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

        _logger.LogInformation($"Subscription {subscription.Id} created for user {userId}");
    }

    private async Task HandleSubscriptionUpdated(Event stripeEvent, string rawJson) {
 // Try to cast to Stripe.Subscription for status and id
 var stripeSubscription = stripeEvent.Data.Object as Stripe.Subscription;
 if (stripeSubscription == null) return;

 var subscription = await _context.Subscriptions
 .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscription.Id);

 if (subscription == null) {
 _logger.LogWarning($"Subscription not found for Stripe ID: {stripeSubscription.Id}");
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

 _logger.LogInformation($"Subscription {subscription.Id} updated to status: {subscription.Status}");
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
 user.CurrentTier = SubscriptionTier.Free;
 user.ActiveSubscriptionId = null;
 }

 await _context.SaveChangesAsync();

 _logger.LogInformation($"Subscription {subscription.Id} deleted/cancelled");
 }

    private async Task HandleInvoicePaymentSucceeded(Event stripeEvent, string rawJson) {
        var invoice = stripeEvent.Data.Object as Stripe.Invoice;
        if (invoice == null) {
            _logger.LogWarning("Invoice is null in payment_succeeded event");
            return;
        }

        _logger.LogInformation($"Processing invoice payment: {invoice.Id}");

        // Get subscription ID from nested parent.subscription_details.subscription
        string? subscriptionId = null;
        string? paymentIntentId = null;

        try {
            using var doc = JsonDocument.Parse(rawJson);
            var obj = doc.RootElement.GetProperty("data").GetProperty("object");

            // Navigate to parent.subscription_details.subscription
            if (obj.TryGetProperty("parent", out var parentProp)) {
                _logger.LogInformation("Found parent property");

                if (parentProp.TryGetProperty("subscription_details", out var subDetailsProp)) {
                    _logger.LogInformation("Found subscription_details property");

                    if (subDetailsProp.TryGetProperty("subscription", out var subProp) &&
                        subProp.ValueKind == JsonValueKind.String) {
                        subscriptionId = subProp.GetString();
                        _logger.LogInformation($"Found subscription ID: {subscriptionId}");
                    }
                }
            }

            // Fallback: Try to get from line items if not in parent
            if (string.IsNullOrEmpty(subscriptionId) && obj.TryGetProperty("lines", out var linesProp)) {
                _logger.LogInformation("Trying to extract subscription from line items");

                if (linesProp.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Array) {
                    var firstLine = dataProp.EnumerateArray().FirstOrDefault();
                    if (firstLine.ValueKind != JsonValueKind.Undefined) {
                        if (firstLine.TryGetProperty("parent", out var lineParentProp) &&
                            lineParentProp.TryGetProperty("subscription_item_details", out var sidProp) &&
                            sidProp.TryGetProperty("subscription", out var lineSubProp) &&
                            lineSubProp.ValueKind == JsonValueKind.String) {
                            subscriptionId = lineSubProp.GetString();
                            _logger.LogInformation($"Found subscription ID in line items: {subscriptionId}");
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

        _logger.LogInformation($"Processing renewal for subscription: {subscriptionId}");

        // Find subscription in database
        var subscription = await _context.Subscriptions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscriptionId);

        if (subscription == null) {
            _logger.LogWarning($"Subscription not found in database: {subscriptionId}");
            return;
        }

        _logger.LogInformation($"Found subscription in database for user: {subscription.UserId}");

        // Record payment
        var paymentHistory = new PaymentHistory {
            UserId = subscription.UserId,
            SubscriptionId = subscription.Id,
            Amount = invoice.AmountPaid / 100m,
            Currency = invoice.Currency?.ToUpper() ?? "USD",
            Status = PaymentStatus.Succeeded,
            Description = $"Initial payment for subscription",
            StripePaymentIntentId = paymentIntentId,
            Type = PaymentType.SubscriptionRecurring,  // This is the correct enum value for renewals
            CreatedAt = DateTime.UtcNow
        };

        _context.PaymentHistory.Add(paymentHistory);
        _logger.LogInformation($"Created payment history record: {invoice.AmountPaid / 100m:C}");

        // Reset monthly downloads for user
        var user = subscription.User;
        if (user != null) {
            user.DownloadsThisMonth = 0;
            user.LastDownloadDate = DateTime.UtcNow;
            _logger.LogInformation($"Reset downloads for user {user.Id}");
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation($"Successfully processed invoice payment for subscription {subscriptionId}");
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

 _logger.LogWarning($"Payment failed for subscription {subscription.Id}");

 // TODO: Send email notification to user
 }
}