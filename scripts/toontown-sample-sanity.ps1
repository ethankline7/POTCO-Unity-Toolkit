param()

$ErrorActionPreference = 'Stop'

Write-Host '=== Toontown Sample Sanity ===' -ForegroundColor Cyan

function Assert-ContentMatch {
  param(
    [string]$Content,
    [string]$Pattern,
    [string]$ErrorMessage
  )

  if ($Content -notmatch $Pattern) {
    throw $ErrorMessage
  }
}

$samplePath = 'Assets/Editor/Toontown/Samples/toontown_sample_world.py'
if (-not (Test-Path $samplePath)) {
  throw "Missing Toontown bundled sample: $samplePath"
}

$content = Get-Content -Raw $samplePath
Assert-ContentMatch -Content $content -Pattern "objectStruct\s*=\s*\{" -ErrorMessage "Bundled sample is missing objectStruct root assignment."
Assert-ContentMatch -Content $content -Pattern "'Objects'\s*:\s*\{" -ErrorMessage "Bundled sample is missing 'Objects' dictionary."
Assert-ContentMatch -Content $content -Pattern "sample-zone-root" -ErrorMessage "Bundled sample is missing expected root object id: sample-zone-root."
Assert-ContentMatch -Content $content -Pattern "phase_4/models/props/mailbox" -ErrorMessage "Bundled sample is missing expected mailbox model mapping."

$assignmentPath = 'Assets/Editor/Toontown/Samples/toontown_sample_world_assignment_style.py'
if (-not (Test-Path $assignmentPath)) {
  throw "Missing Toontown assignment-style sample: $assignmentPath"
}

$assignmentContent = Get-Content -Raw $assignmentPath
Assert-ContentMatch -Content $assignmentContent -Pattern 'objectStruct\["Objects"\]\s*=\s*\{' -ErrorMessage "Assignment sample is missing objectStruct assignment-style objects root."
Assert-ContentMatch -Content $assignmentContent -Pattern 'objectStruct\["Objects"\]\["assign-root"\]\s*=\s*\{' -ErrorMessage "Assignment sample is missing expected root object assignment."
Assert-ContentMatch -Content $assignmentContent -Pattern 'objectStruct\["Objects"\]\["assign-root"\]\["Objects"\]\["assign-prop"\]\["Zone"\]\s*=' -ErrorMessage "Assignment sample is missing direct property assignment coverage."
Assert-ContentMatch -Content $assignmentContent -Pattern "phase_4/models/props/mailbox" -ErrorMessage "Assignment sample is missing expected mailbox model mapping."

$dnaZonePath = 'Assets/Editor/Toontown/Samples/toontown_dna_zone_regression.dna'
if (-not (Test-Path $dnaZonePath)) {
  throw "Missing Toontown DNA zone regression sample: $dnaZonePath"
}

$dnaZoneContent = Get-Content -Raw $dnaZonePath
Assert-ContentMatch -Content $dnaZoneContent -Pattern 'prop\s+"front-door"' -ErrorMessage "DNA zone regression sample is missing expected prop block."
Assert-ContentMatch -Content $dnaZoneContent -Pattern 'code\s*\[\s*"tt_test_door"\s*\]' -ErrorMessage "DNA zone regression sample is missing expected storage code reference."

$dnaStoragePath = 'Assets/Editor/Toontown/Samples/toontown_dna_storage_regression.dna'
if (-not (Test-Path $dnaStoragePath)) {
  throw "Missing Toontown DNA storage regression sample: $dnaStoragePath"
}

$dnaStorageContent = Get-Content -Raw $dnaStoragePath
Assert-ContentMatch -Content $dnaStorageContent -Pattern 'store_node\s*\[' -ErrorMessage "DNA storage regression sample is missing store_node mapping."
Assert-ContentMatch -Content $dnaStorageContent -Pattern 'door_origin_ul' -ErrorMessage "DNA storage regression sample is missing expected resolved node."

Write-Host 'Toontown sample sanity: OK' -ForegroundColor Green
