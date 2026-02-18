# StitchLens Launch Hardening - 2 Week Sprint Plan

## Sprint Goal
Ship a stable, secure, observable MVP to public users with low launch-day risk and clear rollback options.

## Scope Definition

### Must-Have Before Public Launch
- Payment flow correctness and idempotency (Pay As You Go and subscriptions)
- Annual billing explicitly enabled only if annual Stripe price IDs are configured; otherwise disabled by design with safe UX
- Webhook reliability and replay safety
- Critical-path smoke tests and regressions for upload/configure/preview/download/purchase
- Error handling and user-facing fallback messages for payment and PDF generation failures
- Secrets/config hygiene verification for production
- Logging and alerting for critical failures
- Migration backup and rollback runbook
- Basic operational runbook (deploy, verify, rollback, incident triage)
- Launch-day communications templates for incidents and rollback

### Nice-to-Have (If Capacity Allows)
- Load/perf tuning beyond baseline
- Enhanced dashboards and analytics
- Expanded automation for UI tests
- Advanced cache cleanup jobs and retention tuning
- UX polish for edge-case messaging

## Team Assumptions
- 1 engineer + product owner
- Daily check-in and end-of-day status
- Staging environment available with Stripe test mode + webhook endpoint

## Evidence Convention
- Store sprint evidence under `StitchLens.Web/docs/launch-hardening-evidence/`.
- Use one folder per day (`day-01` ... `day-10`) with test output, screenshots, and notes.
- Every deliverable in this plan should include at least one linked artifact.

## Day-by-Day Plan (2 Weeks)

## Week 1 - Correctness, Security, and Core Reliability

### Day 1 (Mon): Hardening Kickoff + Risk Inventory
**Priority:** Must-Have
- Freeze feature scope for launch hardening window.
- Build a risk register with top failure modes:
  - One-time payment bypass/duplication
  - Subscription activation mismatch
  - Webhook failures/replays
  - PDF generation/caching failure
  - Migration/deploy rollback risk
- Define launch acceptance criteria (go/no-go checklist baseline).
- Confirm production config inventory (Stripe keys, webhook secrets, DB connection, logging sink).

**Deliverables**
- Launch Risk Register
- Launch Acceptance Criteria v1

### Day 2 (Tue): Payment Flow Correctness - Pay As You Go
**Priority:** Must-Have
- Verify one-time purchase gating logic end-to-end:
  - unpaid -> Stripe checkout
  - paid -> unlock and no re-charge for same project
  - canceled/failed -> return to preview with clear message
- Add/verify idempotency guards around payment recording:
  - PaymentIntent uniqueness checks
  - duplicate callback safety
- Validate project-level unlock semantics explicitly (not per-download).

**Tests**
- Manual + automated integration checks for paygo purchase lifecycle.
- Negative tests for tampered/invalid session ids.

**Deliverables**
- PayGo Payment Test Matrix
- Pass/fail test report

### Day 3 (Wed): Subscription Checkout + Billing Cycle Correctness
**Priority:** Must-Have
- Verify monthly/annual checkout routing uses DB tier config values.
- Validate subscription metadata flow (`tier`, `billing_cycle`) through checkout and webhook.
- Confirm dashboard/plan labels reflect monthly vs annual.
- Confirm annual-unavailable state behaves safely (no broken checkout) when annual Stripe IDs are missing.
- Record explicit launch decision: annual enabled vs disabled-by-design.

**Deliverables**
- Subscription Flow Validation Report

### Day 4 (Thu): Webhook Reliability + Replay Safety
**Priority:** Must-Have
- Verify webhook signature validation in staging.
- Add explicit handling for event idempotency (ignore already-processed events).
- Validate replay-safe behavior for:
  - `checkout.session.completed`
  - subscription state events
  - duplicate delivery of the same event
  - out-of-order event delivery
  - missing/invalid metadata (`tier`, `billing_cycle`, project identifiers)
  - transient processing failures followed by Stripe retries
- Ensure one-time and subscription sessions are routed to correct handler path.

**Deliverables**
- Webhook Event Handling Matrix
- Webhook Replay Test Evidence

### Day 5 (Fri): Critical-Path Automated Smoke Tests
**Priority:** Must-Have
- Implement/expand smoke tests for:
  - Register/Login
  - Upload -> Configure -> Preview
  - PayGo purchase -> unlock -> PDF download
  - Subscription checkout callback/webhook processing (stub/mock where needed)
- Add CI gate: fail build on critical smoke test failures.

**Deliverables**
- Critical Smoke Test Suite
- CI job + build log output

## Week 2 - Observability, Operations, Performance Baseline, Launch Readiness

### Day 6 (Mon): Error Handling + UX Recovery Paths
**Priority:** Must-Have
- Audit controller actions for failure paths:
  - Stripe exceptions
  - missing/invalid session metadata
  - file read/write failures for PDF/cache
- Standardize user-facing error/success messages.
- Ensure no raw exception text leaks to users in production mode.

**Deliverables**
- Error Handling Checklist marked complete

### Day 7 (Tue): Secrets + Security Hygiene
**Priority:** Must-Have
- Verify no secrets in repo history/worktree.
- Confirm production uses environment/secret manager values only.
- Confirm staging/production config parity for launch-critical settings:
  - Stripe publishable/secret keys
  - Stripe webhook secret
  - DB target/connection string
  - logging sink and alert destinations
  - launch feature flags
- Validate cookie/auth settings for production:
  - secure cookies
  - HTTPS enforcement
  - same-site policy sanity check
- Validate anti-forgery protections on mutation endpoints.
- Validate session timeout and remember-me behavior.
- Validate login lockout/rate limiting for abuse scenarios.
- Validate password reset/account recovery flows.

**Deliverables**
- Security Readiness Checklist
- Evidence notes for each item

### Day 8 (Wed): Observability + Alerting Minimum Viable Ops
**Priority:** Must-Have
- Add structured log events for critical operations:
  - pattern generation start/end/fail
  - payment start/success/fail
  - webhook received/processed/fail
  - PDF cache hit/miss/fail
- Configure baseline alerts:
  - elevated 5xx
  - webhook processing failures
  - payment completion failures
- Add correlation id logging through request pipeline.

**Deliverables**
- Operations Dashboard v1
- Alert Rules v1

### Day 9 (Thu): Migration, Backup, and Rollback Drills
**Priority:** Must-Have
- Practice migration on staging snapshot.
- Validate backup and restore procedure.
- Create rollback steps for:
  - app deploy
  - schema migration
- Time-boxed rollback drill and document timings.

**Deliverables**
- Migration + Rollback Runbook
- Drill results with timestamps

### Day 10 (Fri): Performance Baseline + Launch Go/No-Go
**Priority:** Must-Have + Nice-to-Have
- Baseline key timings:
  - Upload
  - Configure submit
  - Preview load
  - First PDF generation
  - Cached PDF download
  - PayGo purchase completion return
- Define threshold alarms for launch week.
- Complete final go/no-go review against acceptance criteria.
- Build launch-day checklist and owner assignments.
- Assign primary + backup owner for each launch-day task.
- Finalize customer communication templates:
  - degraded service notice
  - payment issue notice
  - rollback notice

**Deliverables**
- Performance Baseline Report
- Go/No-Go Decision Sheet
- Launch Day Checklist

## Nice-to-Have Backlog (Post Sprint or if Ahead)
- Gallery-ready indexing prep for `Public` + `Tags`
- Scheduled cleanup for stale upload and cached PDF artifacts (promote to Must-Have if expected launch storage growth is moderate/high)
- Expanded browser/device matrix for UI validation
- Better billing analytics (funnel from preview -> checkout -> success)
- Admin diagnostics page for payment/webhook event inspection

## Acceptance Criteria (Launch Ready)
- 100% pass on critical smoke tests for 3 consecutive CI runs
- No unresolved P0/P1 issues in risk register
- Payment/webhook replay tests pass with no duplicate side effects
- Rollback drill completed and documented
- Monitoring + alerts active and verified
- Launch-day runbook approved

## Daily Cadence
- 15-minute morning standup:
  - top blockers
  - day target
- End-of-day update:
  - completed items
  - evidence links
  - risk register updates

## Owners (Suggested)
- Engineering: implementation, tests, observability, runbooks
- Product/Founder: acceptance criteria signoff, launch messaging, go/no-go decision
- Shared: launch-day checklist execution

## Launch-Day Ownership Model
- Each critical task must have one primary owner and one backup owner.
- If a primary owner is unavailable, backup owner is empowered to execute without delay.
- Keep owner contact details and escalation order in the launch-day checklist.
