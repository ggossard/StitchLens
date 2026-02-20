# StitchLens Launch Hardening Checklist

Use this as a quick daily execution sheet during the 2-week hardening sprint.

Hosting/deployment execution reference: `StitchLens.Web/docs/aws-production-execution-plan.md`

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
- [-] Upload -> Configure -> Preview flow smoke-tested.
- [-] Download flow verified for each tier.
- [x] PDF cache hit/miss/invalidation behavior verified.
- [x] Error paths return user-friendly messages (no raw exceptions).
- [x] Critical smoke tests pass in CI.

### Security and Configuration
- [-] No secrets in repository/worktree.
- [-] Production secrets sourced from env/secret manager.
- [x] Cookie/auth settings verified for production.
- [x] HTTPS and anti-forgery protections verified.
- [x] Session timeout and remember-me behavior verified.
- [x] Login lockout/rate limiting verified for abusive attempts.
- [x] Password reset and account recovery flow verified.
- [-] Staging-to-production config parity check completed (Stripe keys, webhook secret, DB target, log sink, feature flags).

### Observability and Operations
- [x] Structured logs exist for payment, webhook, generation, and download paths.
- [x] Correlation id visible in logs for critical requests.
- [ ] Alerts configured for 5xx spikes and webhook/payment failures.
- [-] Basic health checks and dashboard are available.

### Database and Deploy Safety
- [ ] Migration tested on staging copy.
- [ ] Backup and restore procedure validated.
- [-] Rollback plan documented and tested (documented in `StitchLens.Web/docs/launch-hardening-evidence/go-no-go/rollback.md`; staging drill pending).
- [-] Deploy runbook drafted and reviewed (drafted in `StitchLens.Web/docs/launch-hardening-evidence/go-no-go/deploy-runbook.md`; review sign-off pending).

### Launch Readiness
- [ ] Go/No-Go criteria agreed and signed off.
- [ ] Launch-day owner assignments complete.
- [ ] Each launch-day task has primary + backup owner.
- [x] Incident triage path documented.
- [x] Customer-facing support response templates prepared.
- [x] Launch-day communication templates prepared (degraded service, payment issue, rollback notice).

### Evidence and Auditability
- [x] Evidence folder prepared for sprint artifacts (tests, screenshots, logs, runbooks).
- [-] Daily evidence links added to tracking log entries.
- [-] Go/No-Go packet includes links to all must-have evidence (packet skeleton in `StitchLens.Web/docs/launch-hardening-evidence/go-no-go/`; evidence-link pass pending).

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
- Focus: observability, security verification, recovery flow hardening
- Completed: launch-critical tests for webhook/payment/idempotency, security config, remember-me, password reset/account recovery, and error handling (see `StitchLens.Web/docs/launch-hardening-evidence/go-no-go/`)
- Blockers: staging-only validations (webhook signature in staging, migration/backup/restore drill)

### Day 9
- Focus: go/no-go packet scaffolding and launch deployment readiness docs.
- Completed: drafted config parity checklist and deploy runbook (`StitchLens.Web/docs/launch-hardening-evidence/go-no-go/config-parity-checklist.md`, `StitchLens.Web/docs/launch-hardening-evidence/go-no-go/deploy-runbook.md`).
- Blockers: staging execution evidence still required (webhook signature validation, migration/backup/restore drill, owner assignments/sign-off).

### Day 10
- Focus: staging safety proofs (webhook signature validation, migration, backup/restore, rollback drill).
- Completed: prepared Day A run sheet and Day 10 evidence folder (`StitchLens.Web/docs/launch-hardening-evidence/go-no-go/day-a-staging-safety-run-sheet.md`, `StitchLens.Web/docs/launch-hardening-evidence/day-10/`), and ran a quick repository secret-hygiene pass (see `StitchLens.Web/docs/launch-hardening-evidence/day-10/commands.txt`).
- Blockers: requires staging environment execution and operator credentials/access for DB and deploy tooling.
