# Toontown Importer Stabilization Brief

Branch:
- `codex/toontown-importer-stabilization`

Working title:
- `fix: stabilize Toontown DNA importer baseline`

## Goal
Make the current Toontown DNA importer easier to trust before adding more feature surface.

This pass should harden the existing MVP path:
- parse DNA inputs
- resolve storage model hints
- isolate resolved nodes conservatively
- keep EGG alpha/material scope from leaking between unrelated geometry
- report demo health in comparable categories
- provide repeatable regression commands

## In Scope
- Strict vs fuzzy resolved-node regression coverage.
- DNA demo metrics that can be copied into PR descriptions.
- Warning category counts for importer health tracking.
- A command-line parser regression runner.
- Lightweight primary-check guards for fragile importer assumptions.

## Out Of Scope
- Unity editor/package upgrade work.
- Broad Toontown visual parity.
- New importer feature sprawl.
- POTCO runtime refactors.
- Generated resource, scene, log, or screenshot commits.

## Current Stabilization Checks
- `scripts\run-action.cmd primary-checks`
- `scripts\run-action.cmd parser-regression`
- `scripts\run-action.cmd dna-demo -SkipResourceSetup`

Use `primary-checks` before every commit. Use the Unity-backed commands when the matching Unity editor is available and the project can be opened without mixing in upgrade churn.

## Definition Of Done
- Primary checks pass.
- Parser regression passes in Unity or the editor blocker is documented.
- DNA demo runs and records metrics, or the blocker is documented.
- No generated resource dump is staged.
- No Unity/package version changes are mixed into this branch.
- PR description includes DNA demo metrics when available.

## Notes
- PR #10 remains the Unity 6000.4.1 upgrade lane.
- Do not merge upgrade changes into this branch unless the project owner explicitly changes the release order.
- If only Unity 6000.4.1 is installed locally, prefer documenting that blocker over silently upgrading project files inside the importer stabilization PR.
