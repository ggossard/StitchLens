using Stripe.Checkout;

namespace StitchLens.Web.Services;

public interface IStripeCheckoutSessionService {
    Task<Session> CreateAsync(SessionCreateOptions options);
    Task<Session> GetAsync(string sessionId);
}
