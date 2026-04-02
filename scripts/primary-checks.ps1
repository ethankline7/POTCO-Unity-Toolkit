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
  'Assets/Editor/Toontown/World Data/ToontownWorldDataImporter.cs',
  'Assets/Editor/Toontown/World Data/ToontownWorldDataExporter.cs'
)

foreach ($path in $requiredToolkitFiles) {
  if (-not (Test-Path $path)) {
    throw "Missing required toolkit file: $path"
  }
}

Write-Host 'Toolkit scaffolding files: OK' -ForegroundColor Green
Write-Host 'Primary checks passed.' -ForegroundColor Green
