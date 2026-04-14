param()

$ErrorActionPreference = 'Stop'

Write-Host '=== Primary Checks ===' -ForegroundColor Cyan

# Run baseline project checks first.
& "$PSScriptRoot/checks.ps1"

# Ensure key toolkit routing/scaffolding files exist.
$requiredToolkitFiles = @(
  'Assets/Editor/Toolkit/WorldData/WorldDataRouterWindow.cs',
  'Assets/Editor/Toolkit/WorldData/WorldDataToolRouteResolver.cs',
  'Assets/Editor/Toolkit/WorldData/WorldDataToolLauncherRegistry.cs',
  'Assets/Editor/Toolkit/WorldData/WorldDataFormatAdapterRegistry.cs',
  'Assets/Editor/Toolkit/WorldData/Adapters/Toontown/ToontownWorldDataDocumentReader.cs',
  'Assets/Editor/Toolkit/WorldData/Adapters/Toontown/ToontownWorldDataDocumentWriter.cs',
  'Assets/Editor/Toontown/Config/ObjectTypeMap.json',
  'Assets/Editor/Toontown/ToontownQuickStartWindow.cs',
  'Assets/Editor/Toontown/ToontownToolkitPaths.cs',
  'Assets/Editor/Toontown/Validation/ToontownSampleSmokeTestRunner.cs',
  'Assets/Editor/Toontown/Validation/ToontownParserRegressionRunner.cs',
  'Assets/Editor/Toontown/Validation/ToontownSampleValidationWindow.cs',
  'Assets/Editor/Toontown/World Data/ToontownWorldDataImporter.cs',
  'Assets/Editor/Toontown/World Data/ToontownWorldDataExporter.cs',
  'Assets/Editor/Toontown/Samples/toontown_sample_world.py',
  'Assets/Editor/Toontown/Samples/toontown_sample_world_assignment_style.py',
  'Assets/Editor/Toontown/Samples/toontown_dna_zone_regression.dna',
  'Assets/Editor/Toontown/Samples/toontown_dna_storage_regression.dna',
  'scripts/unity-version-guard.ps1',
  'scripts/run-toontown-parser-regression.ps1'
)

foreach ($path in $requiredToolkitFiles) {
  if (-not (Test-Path $path)) {
    throw "Missing required toolkit file: $path"
  }
}

Write-Host 'Toolkit scaffolding files: OK' -ForegroundColor Green

# Validate Toontown type-map config JSON shape.
$typeMapPath = 'Assets/Editor/Toontown/Config/ObjectTypeMap.json'
try {
  $rawJson = Get-Content -Raw $typeMapPath
  $jsonObj = $rawJson | ConvertFrom-Json
  if (-not $jsonObj.defaultType) {
    throw "Missing 'defaultType' in $typeMapPath"
  }
  if (-not $jsonObj.rules) {
    throw "Missing 'rules' array in $typeMapPath"
  }
} catch {
  throw "Invalid Toontown type-map config: $($_.Exception.Message)"
}

Write-Host 'Toontown type-map config: OK' -ForegroundColor Green

# Ensure Toontown reader/writer are not left as scaffold-only implementations.
$stubMatches = git grep -n "scaffold and has not been implemented yet" -- `
  "Assets/Editor/Toolkit/WorldData/Adapters/Toontown/ToontownWorldDataDocumentReader.cs" `
  "Assets/Editor/Toolkit/WorldData/Adapters/Toontown/ToontownWorldDataDocumentWriter.cs" 2>$null

if ($LASTEXITCODE -eq 0 -and $stubMatches) {
  Write-Host $stubMatches
  throw "Toontown reader/writer still contains scaffold placeholders."
}

Write-Host 'Toontown reader/writer implementation guard: OK' -ForegroundColor Green

# Keep generated Toontown demo/resource output out of commits. These paths are reproducible
# local artifacts, not source.
$generatedToontownPathspecs = @(
  'Assets/Editor/Toontown/Samples/Generated',
  'Assets/Resources/phase_3.5',
  'Assets/Resources/phase_3/maps/*.png',
  'Assets/Resources/phase_4/maps/*.png',
  'Assets/Resources/phase_5/maps/*.png',
  'Assets/Resources/phase_4/models/modules',
  'Assets/Resources/phase_4/models/neighborhoods',
  'Assets/Resources/phase_5/models/props/TT_hydrant.egg',
  'Assets/Resources/phase_5/models/props/trashcan_TT.egg'
)

$trackedGeneratedToontownFiles = git ls-files -- $generatedToontownPathspecs
if ($trackedGeneratedToontownFiles) {
  Write-Host $trackedGeneratedToontownFiles
  throw 'Generated Toontown resource/demo output is tracked. Remove it from the commit scope.'
}

$stagedGeneratedToontownFiles = git diff --cached --name-only -- $generatedToontownPathspecs
if ($stagedGeneratedToontownFiles) {
  Write-Host $stagedGeneratedToontownFiles
  throw 'Generated Toontown resource/demo output is staged. Unstage it before committing.'
}

Write-Host 'Generated Toontown output guard: OK' -ForegroundColor Green

# Guard the EGG importer behavior that keeps inherited texture and alpha metadata scoped
# to the current group branch instead of leaking into unrelated polygons.
$geometryProcessorPath = 'Assets/Editor/Egg Importer/Processors/GeometryProcessor.cs'
$geometryProcessor = Get-Content -Raw $geometryProcessorPath
$alphaScopeNeedles = @(
  'var scopedTextureRefs = CloneTextureRefs(inheritedTextureRefs);',
  'bool scopedAlphaBlend = inheritedAlphaBlend;',
  'scopedTextureRefs,',
  'scopedAlphaBlend);',
  'bool hasAlphaBlend = inheritedAlphaBlend;'
)

foreach ($needle in $alphaScopeNeedles) {
  if (-not $geometryProcessor.Contains($needle)) {
    throw "EGG alpha/material scope guard failed. Missing expected code in ${geometryProcessorPath}: $needle"
  }
}

Write-Host 'EGG alpha/material scope guard: OK' -ForegroundColor Green

# Validate bundled sample world for first-run quick-start flow.
& "$PSScriptRoot/toontown-sample-sanity.ps1"

Write-Host 'Primary checks passed.' -ForegroundColor Green
