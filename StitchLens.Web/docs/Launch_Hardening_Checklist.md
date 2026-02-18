# StitchLens Launch Hardening Checklist

Use this as a quick daily execution sheet during the 2-week hardening sprint.

## Must-Have Checklist

### Payments and Billing
- [-] Pay As You Go one-time purchase flow verified end-to-end.
- [x] One-time purchase idempotency verified (no duplicate unlock/payment records).
- [-] Subscription monthly flow verified end-to-end.
- [-] Subscription annual flow verified end-to-end (or intentionally disabled with safe UX).
- [-] Annual billing launch decision documented (enabled only if annual Stripe price IDs are configured; otherwise disabled by design).
- [ ] Stripe webhook signature validation verified in staging.
- [x] Webhook replay/idempotency verified.
- [-] Webhook matrix includes duplicate delivery, out-of-order events, missing metadata, and retry/failure handling.

### Reliability and Correctness
- [ ] Upload -> Configure -> Preview flow smoke-tested.
- [ ] Download flow verified for each tier.
- [ ] PDF cache hit/miss/invalidation behavior verified.
- [-] Error paths return user-friendly messages (no raw exceptions).
- [x] Critical smoke tests pass in CI.

### Security and Configuration
- [ ] No secrets in repository/worktree.
- [ ] Production secrets sourced from env/secret manager.
- [-] Cookie/auth settings verified for production.
- [-] HTTPS and anti-forgery protections verified.
- [ ] Session timeout and remember-me behavior verified.
- [-] Login lockout/rate limiting verified for abusive attempts.
- [ ] Password reset and account recovery flow verified.
- [ ] Staging-to-production config parity check completed (Stripe keys, webhook secret, DB target, log sink, feature flags).

### Observability and Operations
- [x] Structured logs exist for payment, webhook, generation, and download paths.
- [x] Correlation id visible in logs for critical requests.
- [ ] Alerts configured for 5xx spikes and webhook/payment failures.
- [-] Basic health checks and dashboard are available.

### Database and Deploy Safety
- [ ] Migration tested on staging copy.
- [ ] Backup and restore procedure validated.
- [ ] Rollback plan documented and tested.
- [ ] Deploy runbook drafted and reviewed.

### Launch Readiness
- [ ] Go/No-Go criteria agreed and signed off.
- [ ] Launch-day owner assignments complete.
- [ ] Each launch-day task has primary + backup owner.
- [x] Incident triage path documented.
- [x] Customer-facing support response templates prepared.
- [x] Launch-day communication templates prepared (degraded service, payment issue, rollback notice).

### Evidence and Auditability
- [x] Evidence folder prepared for sprint artifacts (tests, screenshots, logs, runbooks).
- [ ] Daily evidence links added to tracking log entries.
- [-] Go/No-Go packet includes links to all must-have evidence.

## Nice-to-Have Checklist

### Performance and Scale
- [ ] Baseline timings captured for critical endpoints.
- [ ] Input size limits tuned for large uploads.
- [ ] Disk usage monitoring and cleanup policy in place (promote to must-have if expected launch volume is moderate/high).

### UX and Product Polish
- [ ] Tier messaging consistency pass complete.
- [ ] Edge-case messaging reviewed and polished.
- [ ] Additional browser/device checks complete.

### Product Analytics
- [ ] Funnel metrics defined (preview -> checkout -> success).
- [ ] Basic launch dashboard includes conversion and failure rates.

## Daily Tracking Log

### Day 1
- Focus:
- Completed:
- Blockers:

### Day 2
- Focus:
- Completed:
- Blockers:

### Day 3
- Focus:
- Completed:
- Blockers:

### Day 4
- Focus:
- Completed:
- Blockers:

### Day 5
- Focus:
- Completed:
- Blockers:

### Day 6
- Focus:
- Completed:
- Blockers:

### Day 7
- Focus:
- Completed:
- Blockers:

### Day 8
- Focus:
- Completed:
- Blockers:

### Day 9
- Focus:
- Completed:
- Blockers:

### Day 10
- Focus:
- Completed:
- Blockers:
