using Stripe.Checkout;

namespace StitchLens.Web.Services;

public class StripeCheckoutSessionService : IStripeCheckoutSessionService {
    private readonly SessionService _sessionService = new();

    public Task<Session> CreateAsync(SessionCreateOptions options) {
        return _sessionService.CreateAsync(options);
    }

    public Task<Session> GetAsync(string sessionId) {
        return _sessionService.GetAsync(sessionId);
    }
}
