# Toontown Quick Start

This is the fastest way to launch and use the Toontown tools in this repository.

## 1. Open Unity Project
- Open the repo in Unity (`6000.1.11f1` recommended in README).

## 2. Open Quick Start Window
- Menu: `Toontown/Quick Start`

## 3. Set Game Flavor
- Click `Switch Active Game Flavor to Toontown`.

## 4. Parse Sample Data
1. Open `Toontown Importer` from quick start or menu `Toontown/World Data/Importer`.
2. Click `Use Bundled Sample File`.
3. Click `Parse Preview`.
4. Confirm object count and warnings are shown.

## 5. Export Sample Data
1. Open `Toontown Exporter` from quick start or menu `Toontown/World Data/Exporter`.
2. Click `Use Bundled Sample Source`.
3. Click `Use Suggested Output Path`.
4. Click `Parse And Write`.
5. Confirm round-trip validation appears.

## 6. Validate Parsing Quality
1. Open `Toontown/Validation/Sample Validator`.
2. Click `Validate Both Bundled Samples`.
3. Optionally click `Export CSV Report`.
4. Optionally click `Run Parser Regression Tests` for fixture-based parser checks.

## 7. Run One-Click Smoke Test
- Menu: `Toontown/Validation/Run Sample Smoke Test`
- This runs parse -> export -> re-parse on both bundled samples and prints reports to the Unity console:
  - dictionary-style sample (`toontown_sample_world.py`)
  - assignment-style sample (`toontown_sample_world_assignment_style.py`)

## 8. Run Smoke Test From Terminal (Batch Mode)
- From the repo root:
  - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/run-toontown-smoke.ps1`
- Optional explicit Unity path:
  - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/run-toontown-smoke.ps1 -UnityExePath "C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe"`
- Optional custom log path:
  - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/run-toontown-smoke.ps1 -LogPath "Temp/toontown-smoke-custom.log"`
- Exit codes:
  - `0` = pass
  - non-zero = smoke failure (tail of log is printed)

## Common Pitfalls
- If importer/exporter warns about active game flavor, set `Toolkit/Settings` to `Toontown`.
- If parse count is zero, verify the source file has an `objectStruct['Objects']` dictionary.
- Keep edits scoped and run `scripts/primary-checks.ps1` before committing.
