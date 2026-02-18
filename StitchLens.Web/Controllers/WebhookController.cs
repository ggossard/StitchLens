using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Stripe;
using StitchLens.Data;
using StitchLens.Data.Models;
using StitchLens.Web.Services;

namespace StitchLens.Web.Controllers;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/[controller]")]
public class WebhookController : ControllerBase {
    private readonly StitchLensDbContext _context;
    private readonly IStripeWebhookProcessor _webhookProcessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        StitchLensDbContext context,
        IStripeWebhookProcessor webhookProcessor,
        IConfiguration configuration,
        ILogger<WebhookController> logger) {
        _context = context;
        _webhookProcessor = webhookProcessor;
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
                    await _webhookProcessor.HandleCheckoutCompletedAsync(stripeEvent, json);
                    break;

                case "customer.subscription.updated":
                    await _webhookProcessor.HandleSubscriptionUpdatedAsync(stripeEvent, json);
                    break;

                case "customer.subscription.deleted":
                    await _webhookProcessor.HandleSubscriptionDeletedAsync(stripeEvent, json);
                    break;

                case "invoice.payment_succeeded":
                    await _webhookProcessor.HandleInvoicePaymentSucceededAsync(stripeEvent, json);
                    break;

                case "invoice.payment_failed":
                    await _webhookProcessor.HandleInvoicePaymentFailedAsync(stripeEvent, json);
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
}
