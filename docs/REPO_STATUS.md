# Repo Status

Last reviewed: April 10, 2026, workspace local time.

## Current State
- Active branch: `main`
- Remote state: clean and current with `origin/main`
- Open PR: `#10 chore: prepare Unity 6000.4.1 upgrade` as a draft
- Local generated Toontown resource output is ignored and should stay out of commits
- Stale Toontown branches have been merged, cherry-picked, or deleted

## What Landed Most Recently
- PR #9 merged the Toontown parity pass:
  - one-sided model shadow patch import/export flow
  - material and texture resolution improvements
  - sign/text card prop controls
  - door/window parent-anchor diagnostics
  - generated resource ignore cleanup
  - Toontown environment switcher
- `main` also includes the parser regression fixture guard commit:
  - `fix: guard parser regression fixture reads with structured failure handling`

## What Is Still Risky
- The Toontown DNA importer is useful but still MVP-grade. Imported scenes can still be visually wrong even when checks pass.
- Door/window placement diagnostics show the next blocker is layout parity, especially wall-module window spacing/count behavior.
- Material and texture repair has improved, but it needs focused regression coverage rather than more one-off manual repair passes.
- The EGG importer has known fragile areas: alpha/texture scope handling, multi-texture behavior, and material assignment edge cases.
- Unity 6000.4.1 upgrade is intentionally isolated in draft PR #10 and should not be merged until the project opens, imports, and compiles cleanly in that editor version.

## Recommended Next Main Branch Update
Next branch: `codex/toontown-importer-stabilization`

Goal: turn the current Toontown import path from "promising MVP" into a reliable baseline that we can trust before expanding features.

Definition of done:
- `scripts/primary-checks.ps1` passes
- Toontown smoke flow passes locally or has a documented blocker
- DNA demo import runs, records key metrics, and produces no missing-model regression
- Known review fixes stay covered by focused regression checks
- No Unity/package version changes are mixed into the branch
- No generated resources, logs, screenshots, local IDE files, or demo output are committed

## Keep Separate
- Unity upgrade work: PR #10 only
- Big visual polish: after importer baseline is stable
- New Toontown systems: after importer baseline is stable
- POTCO runtime refactors: only if required for a specific importer/exporter bug
