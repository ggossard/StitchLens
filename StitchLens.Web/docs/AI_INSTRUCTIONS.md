# StitchLens AI Coding Instructions

This file captures how we want to work with an AI coding assistant in this repo.

## How to use this file at session start

Paste this into your first prompt:

```text
Please follow `StitchLens.Web/docs/AI_INSTRUCTIONS.md` for this session.
```

Then immediately load session continuity context from:

- `SESSION_RESUME.md`

If `SESSION_RESUME.md` exists, treat it as the starting checkpoint for branch, latest commit, and next step.

If a request conflicts with this file, follow the explicit user request.

## Project context

- Solution: `StitchLens.sln`
- Web app: `StitchLens.Web` (ASP.NET Core Razor)
- Core logic: `StitchLens.Core` (image processing, quantization, PDF)
- Data layer: `StitchLens.Data` (EF Core, models, seed/init)

Business context docs:

- Strategic plan: `StitchLens.Web/docs/StrategicPlan.md`
- PRD: `StitchLens.Web/docs/StitchLens_PRD.md`
- AWS production plan: `StitchLens.Web/docs/aws-production-execution-plan.md`

## Working style

- Be concise and practical.
- Make small, focused changes unless asked for a larger refactor.
- Prefer existing patterns over introducing new architecture.
- Explain what changed, where, and why.
- Do not add extra dependencies unless clearly justified.

## Source-of-truth rules

- Pricing tiers source of truth: `StitchLens.Data/DbInitializer.cs` (`TierConfiguration` seed data).
- If docs conflict with code, update docs to match code unless told otherwise.
- Keep naming aligned with current tiers: `PayAsYouGo`, `Hobbyist`, `Creator`, `Custom`.

## Code change guidelines

- Preserve current coding style in each file.
- Avoid broad renames or unrelated cleanup during feature/fix work.
- Do not remove existing comments unless they are inaccurate.
- Keep methods readable and avoid clever one-liners.
- Add brief comments only for non-obvious logic.

## Validation and testing

For code changes, run the smallest meaningful checks first, then broader checks as needed.

Preferred command order:

1. Build solution:

```bash
dotnet build StitchLens.sln
```

2. If tests exist or are affected, run them:

```bash
dotnet test StitchLens.sln
```

3. For web behavior changes, run app smoke check:

```bash
dotnet run --project StitchLens.Web
```

If any command cannot be run, say so and provide exact local verify steps.

## Database and migrations

- Do not create migrations unless explicitly requested.
- If model changes require migration, call it out clearly.
- Keep seed data updates intentional and documented.

## Git and commit behavior

- Do not commit unless explicitly asked.
- When asked to commit, use clear messages focused on intent.
- Never commit secrets, keys, or environment-specific credentials.
- After completing a meaningful change set, suggest an appropriate commit message and ask whether to (1) commit and (2) push.
- Treat push as explicit opt-in every time.

## Documentation update policy

When behavior, pricing, or flows change, update relevant docs in the same task when reasonable.

Also keep session continuity current:

- After each commit created in-session, update `SESSION_RESUME.md` with latest branch, commit hash, what changed, and next step.

Most common doc targets:

- `StitchLens.Web/docs/StrategicPlan.md`
- `README.md`
- `StitchLens.Web/docs/StitchLens_PRD.md`

## UI/UX preferences for this project

- Respect existing visual style and component patterns.
- Keep UX straightforward for non-technical craft users.
- Prioritize clarity of labels and terminology (needlepoint vs cross-stitch).
- Avoid introducing flashy redesigns unless requested.

## Ask-vs-assume rule

Default to making a reasonable choice and moving forward.
Only ask a question when blocked by missing required information or when a choice has major product impact.

## Definition of done

A task is done when:

- Requested code/doc changes are complete.
- Build/tests relevant to the change pass, or limitations are stated.
- Any follow-up risks or next steps are listed briefly.
