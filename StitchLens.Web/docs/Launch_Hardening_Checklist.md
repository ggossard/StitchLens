# StitchLens Launch Hardening Checklist

Use this as a quick daily execution sheet during the 2-week hardening sprint.

## Must-Have Checklist

### Payments and Billing
- [ ] Pay As You Go one-time purchase flow verified end-to-end.
- [ ] One-time purchase idempotency verified (no duplicate unlock/payment records).
- [ ] Subscription monthly flow verified end-to-end.
- [ ] Subscription annual flow verified end-to-end (or intentionally disabled with safe UX).
- [ ] Stripe webhook signature validation verified in staging.
- [ ] Webhook replay/idempotency verified.

### Reliability and Correctness
- [ ] Upload -> Configure -> Preview flow smoke-tested.
- [ ] Download flow verified for each tier.
- [ ] PDF cache hit/miss/invalidation behavior verified.
- [ ] Error paths return user-friendly messages (no raw exceptions).
- [ ] Critical smoke tests pass in CI.

### Security and Configuration
- [ ] No secrets in repository/worktree.
- [ ] Production secrets sourced from env/secret manager.
- [ ] Cookie/auth settings verified for production.
- [ ] HTTPS and anti-forgery protections verified.

### Observability and Operations
- [ ] Structured logs exist for payment, webhook, generation, and download paths.
- [ ] Correlation id visible in logs for critical requests.
- [ ] Alerts configured for 5xx spikes and webhook/payment failures.
- [ ] Basic health checks and dashboard are available.

### Database and Deploy Safety
- [ ] Migration tested on staging copy.
- [ ] Backup and restore procedure validated.
- [ ] Rollback plan documented and tested.
- [ ] Deploy runbook drafted and reviewed.

### Launch Readiness
- [ ] Go/No-Go criteria agreed and signed off.
- [ ] Launch-day owner assignments complete.
- [ ] Incident triage path documented.
- [ ] Customer-facing support response templates prepared.

## Nice-to-Have Checklist

### Performance and Scale
- [ ] Baseline timings captured for critical endpoints.
- [ ] Input size limits tuned for large uploads.
- [ ] Disk usage monitoring and cleanup policy in place.

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
