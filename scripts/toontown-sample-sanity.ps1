param()

$ErrorActionPreference = 'Stop'

Write-Host '=== Toontown Sample Sanity ===' -ForegroundColor Cyan

$samplePath = 'Assets/Editor/Toontown/Samples/toontown_sample_world.py'
if (-not (Test-Path $samplePath)) {
  throw "Missing Toontown bundled sample: $samplePath"
}

$content = Get-Content -Raw $samplePath

if ($content -notmatch "objectStruct\s*=\s*\{") {
  throw "Bundled sample is missing objectStruct root assignment."
}

if ($content -notmatch "'Objects'\s*:\s*\{") {
  throw "Bundled sample is missing 'Objects' dictionary."
}

if ($content -notmatch "sample-zone-root") {
  throw "Bundled sample is missing expected root object id: sample-zone-root."
}

if ($content -notmatch "phase_4/models/props/mailbox") {
  throw "Bundled sample is missing expected mailbox model mapping."
}

Write-Host 'Toontown sample sanity: OK' -ForegroundColor Green
