param(
  [string]$ExternalRoot = "External/open-toontown-resources",
  [string]$SourceDna = "External/open-toontown-resources/phase_4/dna/toontown_central_sz.dna",
  [string[]]$StorageDna = @(
    "External/open-toontown-resources/phase_3.5/dna/storage_interior.dna",
    "External/open-toontown-resources/phase_3.5/dna/storage_tutorial.dna",
    "External/open-toontown-resources/phase_4/dna/storage.dna",
    "External/open-toontown-resources/phase_4/dna/storage_TT.dna",
    "External/open-toontown-resources/phase_4/dna/storage_TT_sz.dna",
    "External/open-toontown-resources/phase_5/dna/storage_town.dna",
    "External/open-toontown-resources/phase_5/dna/storage_TT_town.dna"
  ),
  [string]$DestinationResourcesRoot = "Assets/Resources",
  [switch]$CopyPhaseMaps = $true,
  [switch]$Force
)

$ErrorActionPreference = 'Stop'

function Ensure-Path {
  param([string]$Path)
  if (-not (Test-Path $Path)) {
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
  }
}

function Get-QuotedTokens {
  param([string]$Text)
  $quotedRegex = [regex]'"([^"]*)"'
  $matches = $quotedRegex.Matches($Text)
  $tokens = New-Object System.Collections.Generic.List[string]
  foreach ($m in $matches) {
    $tokens.Add($m.Groups[1].Value)
  }
  return $tokens
}

Write-Host "=== Import Toontown DNA Assets ===" -ForegroundColor Cyan

if (-not (Test-Path $ExternalRoot)) {
  throw "External root not found: $ExternalRoot"
}

if (-not (Test-Path $SourceDna)) {
  throw "Source DNA not found: $SourceDna"
}

$bam2eggCmd = Get-Command bam2egg -ErrorAction SilentlyContinue
if (-not $bam2eggCmd) {
  throw "bam2egg was not found in PATH. Install Panda3D tools or add bam2egg to PATH."
}

$codeRegex = [regex]'^\s*code\s*\[\s*"([^"]+)"'
$modelRegex = [regex]'^\s*([A-Za-z_][A-Za-z0-9_]*_model|model)\s+"([^"]+)"\s*\['
$storeRegex = [regex]'^\s*store_node\s*\[(.+)\]'
$directModelRegex = [regex]'^\s*model\s*\[\s*"([^"]+)"'

$codes = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
$directModels = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)

Get-Content $SourceDna | ForEach-Object {
  $line = $_
  $cm = $codeRegex.Match($line)
  if ($cm.Success) {
    [void]$codes.Add($cm.Groups[1].Value)
  }

  $dm = $directModelRegex.Match($line)
  if ($dm.Success) {
    [void]$directModels.Add($dm.Groups[1].Value)
  }
}

$storeMap = @{}
foreach ($storagePath in $StorageDna) {
  if (-not (Test-Path $storagePath)) {
    Write-Host "Skipping missing storage file: $storagePath" -ForegroundColor Yellow
    continue
  }

  $currentModel = $null
  Get-Content $storagePath | ForEach-Object {
    $line = $_
    $mm = $modelRegex.Match($line)
    if ($mm.Success) {
      $currentModel = $mm.Groups[2].Value
      return
    }

    $sm = $storeRegex.Match($line)
    if ($sm.Success -and $currentModel) {
      $tokens = Get-QuotedTokens $sm.Groups[1].Value
      if ($tokens.Count -ge 2) {
        $nodeName = $tokens[1]
        $preferredCode = if ($tokens.Count -ge 3 -and -not [string]::IsNullOrWhiteSpace($tokens[2])) { $tokens[2] } else { $nodeName }
        $storeMap[$preferredCode] = $currentModel
        $storeMap[$nodeName] = $currentModel
      }
    }
  }
}

$modelPaths = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
foreach ($code in $codes) {
  if ($storeMap.ContainsKey($code)) {
    [void]$modelPaths.Add($storeMap[$code])
  }
}
foreach ($dm in $directModels) {
  [void]$modelPaths.Add($dm)
}

Write-Host ("Codes in source DNA: {0}" -f $codes.Count) -ForegroundColor Yellow
Write-Host ("Mapped model paths: {0}" -f $modelPaths.Count) -ForegroundColor Yellow

$converted = 0
$copiedEgg = 0
$missing = New-Object System.Collections.Generic.List[string]
$alreadyPresent = 0

foreach ($modelPath in ($modelPaths | Sort-Object)) {
  if ([string]::IsNullOrWhiteSpace($modelPath)) {
    continue
  }

  $normalizedModelPath = $modelPath.Replace('\', '/').TrimStart('/')
  $sourceEgg = Join-Path $ExternalRoot ($normalizedModelPath + ".egg")
  $sourceBam = Join-Path $ExternalRoot ($normalizedModelPath + ".bam")
  $targetEgg = Join-Path $DestinationResourcesRoot ($normalizedModelPath + ".egg")

  Ensure-Path (Split-Path -Parent $targetEgg)

  if ((Test-Path $targetEgg) -and -not $Force.IsPresent) {
    $alreadyPresent++
    continue
  }

  if (Test-Path $sourceEgg) {
    Copy-Item -LiteralPath $sourceEgg -Destination $targetEgg -Force
    $copiedEgg++
    continue
  }

  if (Test-Path $sourceBam) {
    & bam2egg $sourceBam $targetEgg
    if ($LASTEXITCODE -ne 0) {
      $missing.Add("$normalizedModelPath (bam2egg failed)")
    }
    else {
      $converted++
    }
    continue
  }

  $missing.Add("$normalizedModelPath (no .egg or .bam)")
}

$phases = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
foreach ($modelPath in $modelPaths) {
  $normalized = $modelPath.Replace('\', '/').TrimStart('/')
  $slashIdx = $normalized.IndexOf('/')
  if ($slashIdx -gt 0) {
    $first = $normalized.Substring(0, $slashIdx)
    if ($first -like "phase_*") {
      [void]$phases.Add($first)
    }
  }
}

if ($CopyPhaseMaps.IsPresent) {
  foreach ($phase in ($phases | Sort-Object)) {
    $sourceMaps = Join-Path $ExternalRoot "$phase/maps"
    $destMaps = Join-Path $DestinationResourcesRoot "$phase/maps"
    if (-not (Test-Path $sourceMaps)) {
      continue
    }

    Ensure-Path $destMaps
    Copy-Item -Path (Join-Path $sourceMaps "*") -Destination $destMaps -Recurse -Force
    Write-Host ("Copied maps for {0}" -f $phase) -ForegroundColor Green
  }
}

Write-Host "----------------------------------------" -ForegroundColor DarkGray
Write-Host ("Converted .bam -> .egg: {0}" -f $converted) -ForegroundColor Green
Write-Host ("Copied existing .egg:   {0}" -f $copiedEgg) -ForegroundColor Green
Write-Host ("Already present:        {0}" -f $alreadyPresent) -ForegroundColor Green
Write-Host ("Missing/failed:         {0}" -f $missing.Count) -ForegroundColor Yellow

if ($missing.Count -gt 0) {
  Write-Host "First missing/failed entries:" -ForegroundColor Yellow
  $missing | Select-Object -First 20 | ForEach-Object { Write-Host ("- " + $_) -ForegroundColor Yellow }
}

Write-Host "Toontown DNA asset import prep complete." -ForegroundColor Cyan
