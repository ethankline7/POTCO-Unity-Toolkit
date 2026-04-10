# Repo Status

Last reviewed: April 9, 2026, workspace local time.

## Current Branch
- Active branch: `codex/toontown-parity-placement-texture-pass1`
- Remote state: fetched and current with `origin/codex/toontown-parity-placement-texture-pass1`
- Relationship to `origin/main`: this branch is six commits ahead
- Current mainline focus: Toontown DNA scene-import parity, material/texture resolution, placement diagnostics, and local run-action tooling

## What Is Solid
- Shared world-data routing and adapter contracts are in place under `Assets/Editor/Toolkit/WorldData`.
- POTCO importer/exporter behavior is still routed through legacy-compatible launchers.
- Toontown `.py` reader/writer scaffolding has moved past placeholder status and has sample parse/export validation.
- Toontown `.dna` MVP import exists and can build a Unity hierarchy from OpenToontown sample resources.
- Local validation scripts exist:
  - `scripts/primary-checks.ps1`
  - `scripts/run-toontown-smoke.ps1`
  - `scripts/run-toontown-dna-demo.ps1`
  - `scripts/run-action.cmd`

## Important Local State
- Unity 6000.4.1 package/editor upgrade changes are intentionally parked on `wip/unity-upgrade-hold`. Keep those separate from Toontown parity/runtime commits unless we decide to upgrade the project baseline.
- OpenToontown resources are reproducible local data. They should live under `External/` or generated `Assets/Resources/phase_*` output and stay out of normal commits.
- Generated demo files under `Assets/Editor/Toontown/Samples/Generated/` are validation output, not source.
- Root logs, Codex screenshots, temporary PRC folders, `.slnx`, and `.vscode` files are local workspace artifacts.

## Next Buildable Step
1. Finish and validate the Toontown parity branch as a focused PR/merge candidate.
2. Run `scripts/primary-checks.ps1` before every commit that changes source/docs.
3. Run `scripts/run-action.cmd dna-demo` when checking DNA scene import behavior in Unity.
4. Decide separately whether to adopt the Unity 6000.4.1 upgrade branch.
5. Continue Toontown DNA import work in small PRs: asset lookup, material assignment, prop placement, and scene audit/reporting.

## Definition Of Done For This Branch
- Primary checks pass.
- No generated resource dumps, logs, screenshots, local IDE files, or temporary conversion files are visible in `git status`.
- Any new editor tool is documented in the quick-start path.
- Package/version changes remain isolated unless the branch is explicitly about upgrading Unity.
