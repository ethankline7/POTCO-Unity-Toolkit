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
  'Assets/Editor/Toontown/World Data/ToontownWorldDataImporter.cs',
  'Assets/Editor/Toontown/World Data/ToontownWorldDataExporter.cs'
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
Write-Host 'Primary checks passed.' -ForegroundColor Green
