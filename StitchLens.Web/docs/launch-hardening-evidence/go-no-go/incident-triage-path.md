# Incident Triage Path

## Severity levels
- `P0`: full outage, payment breakage, data integrity risk
- `P1`: major degraded experience, partial checkout failures
- `P2`: non-critical defects with workaround

## Triage flow
1. Detect and acknowledge incident in team channel.
2. Assign incident commander (primary owner, backup if unavailable).
3. Classify severity and impacted surface (payments, webhook, generation, download).
4. Stabilize user impact (banner, feature toggle, rollback if required).
5. Execute fix-forward or rollback using runbook.
6. Post status update every 15-30 minutes until resolved.

## Escalation triggers
- Escalate to `P0` if duplicate billing, persistent 5xx spikes, or inability to generate/download purchased patterns.

## Closure criteria
- Error rate back to baseline
- Payment/webhook queue healthy
- Customer comms posted
- Follow-up ticket created
