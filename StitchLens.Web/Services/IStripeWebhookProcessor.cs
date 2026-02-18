using Stripe;

namespace StitchLens.Web.Services;

public interface IStripeWebhookProcessor {
    Task HandleCheckoutCompletedAsync(Event stripeEvent, string rawJson);
    Task HandleSubscriptionUpdatedAsync(Event stripeEvent, string rawJson);
    Task HandleSubscriptionDeletedAsync(Event stripeEvent, string rawJson);
    Task HandleInvoicePaymentSucceededAsync(Event stripeEvent, string rawJson);
    Task HandleInvoicePaymentFailedAsync(Event stripeEvent, string rawJson);
}
