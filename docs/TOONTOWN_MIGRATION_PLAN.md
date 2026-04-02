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
- POTCO importer/exporter code paths are unchanged.

## Phase 3 - Toontown Adapters
- Implement Toontown world reader.
- Implement Toontown world writer.
- Implement Toontown object type mapping source.
- Implement Toontown-specific property handlers.

## Phase 4 - Validation
- Import baseline sample world.
- Export round-trip sample and verify structure.
- Add migration notes and troubleshooting docs.

## Non-Goals (Current Phase)
- Full gameplay parity.
- Character system parity.
- Replacing POTCO runtime systems.
