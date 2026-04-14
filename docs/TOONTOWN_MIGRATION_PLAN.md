# Toontown Port Plan

## Goal
Port reusable toolkit infrastructure from POTCO-specific workflows to a game-flavor architecture that can support Toontown Online data pipelines.

## Operator Notes
- Keep POTCO behavior stable while Toontown support is introduced.
- Prefer additive scaffolding before refactors.
- If uncertain, document the assumption first, then implement.

## Phase 1 - Foundation
- Add game-flavor configuration (`POTCO`, `Toontown`).
- Add a single settings surface in Unity editor.
- Add Toontown importer/exporter scaffolding windows.
- Keep existing POTCO tools intact.

## Phase 2 - Shared Core Extraction
- Extract generic parsing, asset lookup, and coordinate helpers into shared utilities.
- Move POTCO-specific logic behind POTCO adapters.
- Define interfaces for world object parsing and serialization.

### Phase 2 Progress
- Added a shared world-data routing layer under `Assets/Editor/Toolkit/WorldData`.
- Added route interfaces and game-specific route providers (`POTCO`, `Toontown`).
- Added `Toolkit/World Data/Router` window to open the active importer/exporter safely.
- Added tool-launcher adapters so router calls go through shared abstractions instead of direct UI menu calls.
- Added shared menu entry points (`Toolkit/World Data/Open Importer`, `Open Exporter`) that delegate to the active launcher.
- Added shared world-data contracts (`IWorldDataDocumentReader`, `IWorldDataDocumentWriter`, `IWorldDataFormatAdapter`).
- Added POTCO and Toontown format adapter scaffolds behind a shared registry.
- POTCO importer/exporter code paths are unchanged.

## Phase 3 - Toontown Adapters
- Implement Toontown world reader.
- Implement Toontown world writer.
- Implement Toontown object type mapping source.
- Implement Toontown-specific property handlers.

### Phase 3 Progress
- Implemented initial Toontown reader pass for `.py` dictionary-style world data (best-effort object extraction).
- Wired Toontown importer window to parse and preview object summaries through shared contracts.
- Implemented initial Toontown writer pass for `WorldDataDocument` output (`objectStruct` + nested `Objects`).
- Wired Toontown exporter window for parse-and-write round-trip scaffolding.
- Added warning surfacing in Toontown importer/exporter for low-confidence parse scenarios.
- Implemented Toontown object type mapping source via `Assets/Editor/Toontown/Config/ObjectTypeMap.json`.
- Added exporter round-trip count validation (re-parse exported output and compare likely object counts).
- Added quick access buttons in Toontown importer/exporter to open the type-map config.
- Added Toontown property normalization and preferred write-order rules for cleaner exports.
- Routed Toontown exporter parse/write operations through shared adapter contracts when Toontown flavor is active.
- Added `Toontown/Validation/Sample Validator` for parse quality benchmarking (unknown type ratio, duplicates, warnings).
- Added CSV report export from validator for tuning evidence and iterative mapping comparisons.
- Hardened reader scope handling so candidate objects are only collected from `Objects` dictionary regions (reduces metadata false positives).
- Added multi-line continuation parsing for bracket/parenthesis property values to improve extraction from wrapped world-data fields.
- Added bundled sample world and one-click quick-start actions across importer/exporter/validator for first-run usability.
- Added bundled sample sanity guard in primary checks to keep quick-start inputs stable across future commits.
- Added `Toontown/Validation/Run Sample Smoke Test` to run parse/export/re-parse verification in one menu action.
- Updated smoke test runner to be batch-safe (no modal dialog in headless execution).
- Updated smoke test runner to validate both bundled sample formats (dictionary-style and assignment-style) in one run.
- Expanded reader parsing support for assignment-style `objectStruct[...]` path assignments and quoted-key variants.
- Normalized quoted scalar values in parsed Toontown properties so direct assignment-style fields round-trip as document values.
- Added parser regression runner (`Toontown/Validation/Run Parser Regression Tests`) with fixture assertions for dictionary and assignment sample formats.
- Expanded validator UX with `Validate Both Bundled Samples` and clearer overall PASS/WARN/FAIL status messaging.
- Added `.dna` document reader MVP (`toontown.dna.zone`) with storage mapping support from `storage*.dna` files.
- Added `Toontown/World Data/DNA Scene Importer (MVP)` to parse `.dna` and build Unity hierarchy with model instantiation attempts.
- Added quick-start documentation for OpenToontown resource clone + first DNA import workflow.
- Hardened module resolved-node alias matching for Toontown DNA imports so model-root aliases can resolve without broadening strict lookup behavior.
- Added export/import rendering patch flow for one-sided model handling (`DoubleSidedShadows`) so imported scenes can force two-sided shadow casting and cull-off material copies when flagged.
- Added a sign/text card prop system for world imports (`SignFrame`/`SignImage`) with per-object toggle control for 2D card props vs replacement-prep visibility.
- Added door/window parent-anchor placement pass with diagnostics so module props can align against parent model anchor nodes more closely to source DNA behavior.
- Added generic parent-anchor alias fallback for prefixed landmark building door origins while leaving wall/window count placement as a diagnostic.
- Split wall/window count-layout diagnostics from anchor lookup misses and skip zero-count window groups during import.
- Added Unity-backed EGG material-scope regression coverage for scoped `<TRef>` inheritance and alpha blend isolation.
- Added `Toontown/Environment Switcher` for quick editor-side skybox, lighting, fog, ambient audio, and effect preset passes while reviewing imported DNA scenes.

## Phase 4 - Validation
- Import baseline sample world.
- Export round-trip sample and verify structure.
- Add migration notes and troubleshooting docs.

### Phase 4 Progress
- Added a scripted Unity batch wrapper for the Toontown scene material audit, including optional `_MainTex` threshold enforcement for future baseline tightening.
- Expanded the scene material audit to print affected renderer hierarchy paths so Default-Material cleanup can be grouped by model family instead of guessed from aggregate counts.
- Reduced wall/window count-layout diagnostics by keeping single-window wall groups on the normal parent-anchor path and applying width-based spacing fallback for multi-window wall groups when parent width data is available.
- Matched OpenLevelEditor wall-style parity for narrow spans by clamping wall/window layout requests to a single centered window when parent width is below `15.0`.
- Pinned higher authored `window_count` layout coverage in parser regression so wide wall spans keep working for four-window style cases seen in OpenLevelEditor style files.
- Swapped importer fallback `_MainTex` assignment to a serializable asset-backed blank texture so `Default-Material` sub-assets no longer lose their texture reference across reimport and audit passes.

### Phase 4 Recommended Next Pass
- Branch from `main` as `codex/toontown-importer-stabilization`.
- Stabilize the current Toontown DNA importer before adding more feature surface.
- Keep strict vs fuzzy resolved-node matching covered in Unity-backed parser regression.
- Keep narrow-wall `window_count` parity covered in Unity-backed parser regression so style-authoring rules stay aligned with OpenLevelEditor.
- Keep expanding validation around EGG alpha/material scope so texture/material definitions cannot leak into unrelated geometry.
- Capture DNA demo metrics in PR descriptions so visual/import quality changes are comparable between runs.
- Keep Unity/package upgrade work isolated in draft PR #10 until editor import and compile behavior is confirmed.

### Missing Goals To Add
- Add numeric quality gates for the importer baseline so "checks pass" is not the only success signal.
- Add a visual golden-scene baseline for the primary DNA demo review scene so parity regressions are easier to spot.
- Add a scratch-scene policy so local review scenes stay out of PR scope unless intentionally promoted to tracked fixtures.
- Add a failure-class coverage map showing which importer risks are protected by automated regression and which still rely on manual review.
- Add explicit merge sequencing for PR #10 so the Unity upgrade only lands after the stabilization branch is merged and validated.

### Stabilization Quality Gates
- `scripts/primary-checks.ps1` passes before every push.
- `scripts/run-action.cmd parser-regression` passes on the project-pinned Unity editor, or the blocker is documented in PR #11.
- `scripts/run-action.cmd dna-demo -SkipResourceSetup` passes on the project-pinned Unity editor, or the blocker is documented in PR #11.
- `scripts/run-action.cmd material-audit` is rerun after any importer/material change that could affect `_MainTex` or `Default-Material` counts.
- Missing models stays at `0`.
- Resolved-node isolate failures stays at `0`.
- Window count-layout pending warnings trend downward from the current baseline instead of growing silently.
- Material-audit `_MainTex` offenders stay at `0`.
- No generated resource dumps, demo output scenes, logs, screenshots, or local scratch scenes are committed unless intentionally promoted as fixtures.

### Visual Baseline
- Treat the current Toontown DNA demo scene as the primary visual review surface for stabilization work.
- When importer behavior changes, capture the current DNA demo metrics in PR #11 and note visible scene differences in the PR thread.
- Prioritize visible review for door/window spacing, landmark entrances, tunnel walls, and known `_MainTex` offender families.

### Scratch Scene Policy
- Keep untracked local review scenes out of commits by default.
- Only promote a local scene to a tracked fixture when it is required for repeatable validation or documentation.
- If a scratch scene is needed during debugging, either keep it untracked or move it under a clearly named generated/samples path before discussing promotion.

### Failure-Class Coverage Map
- Covered by automated regression: parser dictionary and assignment samples, DNA storage mapping, strict/fuzzy resolved-node lookup, module alias matching, parent-anchor alias matching, zero-count window group handling, EGG material-scope inheritance.
- Covered by scripted validation but still needs visual review: DNA demo metrics, warning category counts, material audit offender grouping.
- Still primarily manual and should be reduced over time: wall-module window spacing/count layout parity, scene-level visual placement fidelity, material cleanup for specific offender families.

### Tonight Execution Order (April 13, 2026)
1. Clean the local worktree and confirm which untracked files are intentional scratch artifacts before any merge or deletion step.
2. Rerun `primary-checks`, parser regression, DNA demo, and material audit sequentially on the pinned Unity editor to refresh the current baseline.
3. Update PR #11 with the latest metrics so tonight's work is anchored to one authoritative baseline.
4. Take the first remaining importer gap as the next vertical slice: window count-layout parity for one representative module family.
5. Add or tighten a regression fixture for that layout case before widening the implementation.
6. Implement the smallest importer change that reduces layout warnings without broadening lookup behavior unsafely.
7. Rerun parser regression and DNA demo, then record whether window count-layout warnings decreased, stayed flat, or regressed.
8. Take the first material offender cluster from the audit output and do one focused cleanup pass for that family only.
9. Rerun material audit and capture the new offender count and top remaining clusters in PR #11.
10. If the branch is stable at the end of the night, prepare PR #11 for ready review; keep PR #10 isolated and do not merge or delete either branch until stabilization is intentionally landed.

### Merge Sequencing
- Merge PR #11 before revisiting PR #10.
- After PR #11 lands, switch back to `main`, pull, and delete only branches that are confirmed merged.
- Keep PR #10 as a draft until the project opens, imports, and compiles cleanly in Unity `6000.4.1f1`.

## Non-Goals (Current Phase)
- Full gameplay parity.
- Character system parity.
- Replacing POTCO runtime systems.
