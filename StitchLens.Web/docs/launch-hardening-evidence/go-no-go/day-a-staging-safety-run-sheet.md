# Day A Run Sheet - Staging Safety Proofs

Use this checklist to execute and document the hard blockers first.

Operator command reference: `StitchLens.Web/docs/launch-hardening-evidence/go-no-go/day-a-operator-command-pack.md`

## Scope
- Webhook signature validation in staging
- Migration test on staging copy
- Backup and restore validation
- Rollback drill validation

## Roles
- Driver:
- Verifier:
- Incident channel:

## Pre-flight (15 min)
- [ ] Confirm target environment is staging.
- [ ] Confirm deployable commit hash:
- [ ] Confirm maintenance/test window approved.
- [ ] Confirm access to DB backup/restore tooling.
- [ ] Confirm log sink/dashboard access.

## 1) Webhook signature validation (30-45 min)

### Steps
1. Trigger a test Stripe webhook event against staging endpoint.
2. Verify signature validation succeeds for valid signature.
3. Send a request with invalid signature and verify rejection path.
4. Confirm structured logs capture expected outcome (success/failure) with correlation/request id.

### Record
- Staging webhook endpoint:
- Valid event id:
- Invalid signature test id:
- Result: PASS/FAIL

### Evidence to attach
- [ ] Screenshot/log showing valid signature accepted
- [ ] Screenshot/log showing invalid signature rejected
- [ ] Log excerpt with correlation/request id

## 2) Migration test on staging copy (30-60 min)

### Steps
1. Point to staging-copy database target.
2. Run migration command:

```bash
dotnet ef database update --project StitchLens.Data --startup-project StitchLens.Web
```

3. Verify migration completed without errors.
4. Run quick app smoke for schema-dependent paths (login + project read).

### Record
- DB target used:
- Migration command result: PASS/FAIL
- Smoke check result: PASS/FAIL

### Evidence to attach
- [ ] Command output snippet
- [ ] Smoke check output/screenshot

## 3) Backup and restore drill (45-90 min)

### Steps
1. Perform backup using staging DB process/tool.
2. Restore to a disposable validation DB.
3. Validate critical tables/rows are present (users, projects, payments).
4. Capture restore duration and any warnings.

### Record
- Backup artifact id/location:
- Restore target:
- Restore duration:
- Result: PASS/FAIL

### Evidence to attach
- [ ] Backup job output
- [ ] Restore job output
- [ ] Validation query output/screenshot

## 4) Rollback drill (30-60 min)

Reference: `StitchLens.Web/docs/launch-hardening-evidence/go-no-go/rollback.md`

### Steps
1. Announce drill start in launch/incident channel.
2. Redeploy previous known-good build to staging.
3. Verify health endpoint(s) and critical user flow.
4. Announce drill completion and status.

### Record
- From version:
- To version:
- Health checks: PASS/FAIL
- Critical flow check: PASS/FAIL
- Result: PASS/FAIL

### Evidence to attach
- [ ] Deploy logs link
- [ ] Health check output
- [ ] Smoke flow output
- [ ] Start/finish channel messages

## Closeout (20 min)
- [ ] Update `StitchLens.Web/docs/Launch_Hardening_Checklist.md` statuses.
- [ ] Update `StitchLens.Web/docs/launch-hardening-evidence/go-no-go/decision.md` blockers/status.
- [ ] Fill `StitchLens.Web/docs/launch-hardening-evidence/day-10/notes.md` (or current day folder).
- [ ] Add all evidence links to checklist day log and go/no-go packet.

## Day A Pass Criteria
- [ ] Webhook signature validation in staging marked complete.
- [ ] Migration test on staging copy marked complete.
- [ ] Backup and restore procedure marked complete.
- [ ] Rollback plan documented and tested marked complete.
- [ ] Remaining blockers are non-P0 only.

## Escalation rule
- If any step above fails, pause onward steps, log blocker, and open/assign fix owner before continuing.
