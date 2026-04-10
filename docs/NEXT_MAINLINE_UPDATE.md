# Next Mainline Update

## Recommendation
The next `main` update should be a Toontown importer stabilization pass, not the Unity upgrade and not another feature burst.

Branch name:
- `codex/toontown-importer-stabilization`

Working title:
- `fix: stabilize Toontown DNA importer baseline`

## Why This Comes Next
The repo has enough Toontown importer functionality to be useful, but the code is still uneven. More features will make the project harder to reason about until the current import path has better guardrails.

This branch should make the existing flow easier to trust:
- parse `.dna`
- resolve models and nodes
- assign materials/textures
- place important props
- report failures clearly
- avoid committing generated asset dumps

## Scope
- Add focused regression checks for strict resolved-node matching and fuzzy fallback behavior.
- Add or improve checks around EGG alpha/material scope so transparent metadata does not leak into opaque geometry.
- Make DNA demo output easier to compare between runs.
- Turn recurring importer warnings into clear categories:
  - missing model
  - missing resolved node
  - fallback placement
  - material fallback
  - fake shadow removal
- Update quick-start docs only where behavior actually changes.

## Out Of Scope
- Unity 6000.4.1 upgrade. Keep that in PR #10.
- New gameplay systems.
- Broad POTCO refactors.
- Full Toontown visual parity.
- Large asset commits from `External/` or generated `Assets/Resources/phase_*` output.

## Acceptance Criteria
- `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/primary-checks.ps1` passes.
- If Unity is available, `scripts\run-action.cmd dna-demo` completes or the blocker is documented.
- `git status --short` shows no generated resource dump.
- The PR description includes current DNA demo metrics.
- Any fix that changes matching/material behavior has a regression or a clear validation note.

## First Tasks
1. Create `codex/toontown-importer-stabilization` from `main`.
2. Add focused parser/importer regression checks for the two recent PR #9 review fixes.
3. Run the DNA demo and record current metrics.
4. Fix the highest-signal importer issue found by the demo.
5. Open a small PR and avoid touching package/editor versions.
