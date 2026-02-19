# Staging-to-Production Config Parity Checklist

Mark each item after verifying both environments have correct values and intended differences are documented.

- [ ] DB target parity: `ConnectionStrings:DefaultConnection` points to correct DB per environment.
- [ ] Stripe keys parity: `Stripe:SecretKey` present and environment-specific.
- [ ] Webhook secret parity: `Stripe:WebhookSecret` present and matches current webhook listener endpoint.
- [ ] `Stripe:PriceIds:HobbyistMonthly` configured.
- [ ] `Stripe:PriceIds:CreatorMonthly` configured.
- [ ] Annual price IDs decision documented:
  - [ ] annual enabled with valid IDs
  - [ ] annual disabled-by-design with safe UX path
- [ ] Email settings configured (`Email:Smtp:*`) or approved provider fallback configured.
- [ ] Log sink parity: logging sink/destination configured and reachable.
- [ ] Alert destinations configured (email/Slack/on-call channel).
- [ ] Feature flags parity checked.
- [ ] Launch-time overrides documented.

## Verification metadata
- Verified by:
- Date:
- Evidence links:
