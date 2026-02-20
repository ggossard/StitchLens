# Day A Operator Command Pack (Placeholders)

Use this as a copy/paste pack for staging safety proofs. Replace placeholders before running.

## Placeholder values
- `<STAGING_BASE_URL>` (example: `https://staging.example.com`)
- `<WEBHOOK_SECRET_VALID>` (staging Stripe webhook secret)
- `<WEBHOOK_SECRET_INVALID>` (any wrong value)
- `<RAW_WEBHOOK_PAYLOAD_JSON>` (exact JSON payload used for signature generation)
- `<RAW_WEBHOOK_PAYLOAD_FILE>` (example: `webhook_payload.json`)
- `<SIGNATURE_VALID>` (computed Stripe-Signature for valid secret)
- `<SIGNATURE_INVALID>` (computed Stripe-Signature for invalid secret)
- `<STAGING_COPY_DB_CONNECTION>`
- `<RESTORE_TARGET_DB_CONNECTION>`
- `<KNOWN_GOOD_RELEASE_ID>`
- `<CURRENT_RELEASE_ID>`

## 1) Webhook signature validation

### Valid signature should succeed
```bash
curl -i -X POST "<STAGING_BASE_URL>/webhook" \
  -H "Content-Type: application/json" \
  -H "Stripe-Signature: <SIGNATURE_VALID>" \
  --data-binary "@<RAW_WEBHOOK_PAYLOAD_FILE>"
```

### Invalid signature should fail
```bash
curl -i -X POST "<STAGING_BASE_URL>/webhook" \
  -H "Content-Type: application/json" \
  -H "Stripe-Signature: <SIGNATURE_INVALID>" \
  --data-binary "@<RAW_WEBHOOK_PAYLOAD_FILE>"
```

Record in evidence:
- HTTP status/body for valid call
- HTTP status/body for invalid call
- corresponding structured log entries with correlation/request id

## 2) Migration test on staging copy

```bash
dotnet ef database update \
  --project StitchLens.Data \
  --startup-project StitchLens.Web \
  --connection "<STAGING_COPY_DB_CONNECTION>"
```

Then run app smoke checks:
```bash
curl -i "<STAGING_BASE_URL>/health/live"
curl -i "<STAGING_BASE_URL>/health/ready"
```

Record in evidence:
- migration command output
- health/live + health/ready output

## 3) Backup and restore validation

Run your platform-specific backup command with the staging source DB, then restore into a disposable target DB.

Pseudo sequence:
```text
backup --source "<STAGING_COPY_DB_CONNECTION>" --out "<BACKUP_ARTIFACT>"
restore --in "<BACKUP_ARTIFACT>" --target "<RESTORE_TARGET_DB_CONNECTION>"
validate --db "<RESTORE_TARGET_DB_CONNECTION>" --checks "users,projects,payments"
```

Record in evidence:
- backup output
- restore output
- data validation output and restore duration

## 4) Rollback drill

Reference: `StitchLens.Web/docs/launch-hardening-evidence/go-no-go/rollback.md`

Pseudo sequence:
```text
deploy --release "<KNOWN_GOOD_RELEASE_ID>" --env staging
healthcheck --url "<STAGING_BASE_URL>/health/live"
healthcheck --url "<STAGING_BASE_URL>/health/ready"
smoke --flows "login,upload-preview,payg-init,subscription-init,webhook-reachable"
deploy --release "<CURRENT_RELEASE_ID>" --env staging
```

Record in evidence:
- deploy log links (rollback + return-forward)
- health check output
- smoke output
- start/finish messages in incident channel

## After execution
- Update `StitchLens.Web/docs/Launch_Hardening_Checklist.md`
- Update `StitchLens.Web/docs/launch-hardening-evidence/go-no-go/decision.md`
- Update `StitchLens.Web/docs/launch-hardening-evidence/day-10/notes.md`
- Append exact commands/outcomes to `StitchLens.Web/docs/launch-hardening-evidence/day-10/commands.txt`
