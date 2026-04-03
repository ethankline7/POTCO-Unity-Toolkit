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
- Added parser regression runner (`Toontown/Validation/Run Parser Regression Tests`) with fixture assertions for dictionary and assignment sample formats.
- Expanded validator UX with `Validate Both Bundled Samples` and clearer overall PASS/WARN/FAIL status messaging.
- Added `.dna` document reader MVP (`toontown.dna.zone`) with storage mapping support from `storage*.dna` files.
- Added `Toontown/World Data/DNA Scene Importer (MVP)` to parse `.dna` and build Unity hierarchy with model instantiation attempts.
- Added quick-start documentation for OpenToontown resource clone + first DNA import workflow.
- Added export/import rendering patch flow for one-sided model handling (`DoubleSidedShadows`) so imported scenes can force two-sided shadow casting and cull-off material copies when flagged.
- Added a sign/text card prop system for world imports (`SignFrame`/`SignImage`) with per-object toggle control for 2D card props vs replacement-prep visibility.

## Phase 4 - Validation
- Import baseline sample world.
- Export round-trip sample and verify structure.
- Add migration notes and troubleshooting docs.

## Non-Goals (Current Phase)
- Full gameplay parity.
- Character system parity.
- Replacing POTCO runtime systems.
