# Launch Hardening Evidence

Store all launch-hardening artifacts here so go/no-go decisions are traceable.

## Folder convention

- `day-01` through `day-10`: daily evidence folders
- `go-no-go`: final launch decision packet

## Daily artifact checklist

Each day folder should include:

- `notes.md`: what was done, risks, and follow-ups
- `commands.txt`: key commands executed and outcomes
- `screenshots/` (optional): UI/console evidence
- links to any related PRs/commits

## Naming guidance

- Prefer short, sortable names, e.g. `01-webhook-replay-pass.png`
- Keep one artifact per concern when possible

## Minimum go/no-go packet

Under `go-no-go/`, include:

- `decision.md` with pass/fail status by criterion
- `owners.md` with primary + backup owner map
- `rollback.md` with tested rollback steps
- links to all required day artifacts
