# Rollback Runbook (Launch)

## Preconditions
- Confirm current deployed version and timestamp
- Confirm backup/migration state

## Rollback steps
1. Announce rollback start in incident channel.
2. Redeploy previous known-good version.
3. Verify app health endpoint.
4. Verify critical user flow:
   - Login
   - Upload -> preview
   - One-time purchase path reachable
   - Webhook endpoint reachable
5. Announce rollback complete.

## Verification evidence
- Link deploy logs:
- Link smoke-test output:
- Link customer comms:

## Post-rollback actions
- Open incident follow-up ticket
- Capture root-cause hypothesis
- Define fix-forward criteria
