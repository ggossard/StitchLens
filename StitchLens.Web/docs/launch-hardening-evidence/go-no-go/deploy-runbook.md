# Deploy Runbook (Launch)

## Preconditions
- Confirm branch/commit hash to deploy.
- Confirm DB backup completed within launch window.
- Confirm required env/config values are present.
- Confirm payment/webhook endpoint target values.

## Deployment steps
1. Announce deployment start in launch channel.
2. Deploy release artifact to target environment.
3. Wait for app startup and health checks:
   - `GET /health/live`
   - `GET /health/ready`
4. Run smoke checks:
   - Login
   - Upload -> configure -> preview
   - Pay-as-you-go checkout initiation
   - Subscription checkout initiation
   - Webhook endpoint reachability
5. Monitor logs/errors for 15 minutes.

## Success criteria
- Health checks green.
- Smoke checks pass.
- No elevated 5xx/webhook failure errors.

## Failure criteria and fallback
- If smoke checks fail or 5xx spike is sustained, execute `rollback.md`.

## Evidence to attach
- Deploy logs link
- Health check screenshots/output
- Smoke test output
- Final status update message
