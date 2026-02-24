# Session Resume

Use this file to quickly resume work after interruptions.

## Resume Prompt

```text
Please follow `StitchLens.Web/docs/AI_INSTRUCTIONS.md` for this session and resume from `SESSION_RESUME.md`.
```

## Current Snapshot

- Date: 2026-02-24
- Branch: `launch-hardening`
- Last pushed commit: use `git rev-parse --short HEAD`
- Focus area: CI stabilization for GitHub Dotnet Tests

## Recent Completed Work

- Added AWS production planning docs and command-level runbook.
- Added AWS deploy templates/scripts under `StitchLens.Web/deploy/aws/`.
- Stabilized CI around initializer concurrency/race conditions.
- Hardened observability endpoint tests for redirect behavior.
- Guarded external OAuth provider registration so missing Google/Facebook secrets do not crash health endpoint tests.

## Key Docs

- `StitchLens.Web/docs/AI_INSTRUCTIONS.md`
- `StitchLens.Web/docs/aws-production-execution-plan.md`
- `StitchLens.Web/docs/aws-command-runbook.md`
- `StitchLens.Web/docs/Launch_Hardening_Checklist.md`

## Next Suggested Step

- Check latest GitHub Actions `Dotnet Tests` run for the current HEAD commit and confirm green.

## Maintenance Rule

- Update this file after every commit created in-session:
  - current branch
  - new commit hash
  - 1-3 bullets of what changed
  - immediate next step
