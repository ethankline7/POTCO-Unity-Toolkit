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
  [switch]$Force,
  [ValidateSet("Bam", "Egg")]
  [string]$SourcePreference = "Bam",
  [bool]$WriteManifest = $true,
  [string]$ManifestPath = "Assets/Editor/Toontown/Samples/Generated/toontown_dna_asset_manifest.json",
  [switch]$FailOnTexturelessEgg
)

$ErrorActionPreference = 'Stop'

function Ensure-Path {
  param([string]$Path)
  if (-not (Test-Path $Path)) {
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
  }
}

function Convert-ToPrcPath {
  param([string]$Path)
  if ([string]::IsNullOrWhiteSpace($Path)) {
    return ""
  }
  return ([System.IO.Path]::GetFullPath($Path)).Replace('\', '/')
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

function Get-EggTextureStats {
  param([string]$EggPath)

  $stats = @{
    TextureDefinitions = 0
    TextureReferences = 0
    MapPhases = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
  }

  if (-not (Test-Path $EggPath)) {
    return $stats
  }

  Get-Content -Path $EggPath | ForEach-Object {
    $line = $_.Trim()
    if ($line.StartsWith("<Texture>")) {
      $stats.TextureDefinitions++
    }
    elseif ($line.StartsWith("<TRef>")) {
      $stats.TextureReferences++
    }

    if ($line -match '"(phase_[^/"]+)/maps/[^"]+"') {
      [void]$stats.MapPhases.Add($matches[1])
    }
  }

  return $stats
}

Write-Host "=== Import Toontown DNA Assets ===" -ForegroundColor Cyan
Write-Host ("Source preference: {0} (BAM keeps more original material/texture references)" -f $SourcePreference) -ForegroundColor Yellow

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

# Ensure Panda tools can resolve referenced textures when converting BAM -> EGG.
# Without this model-path setup, bam2egg strips texture refs and models import white in Unity.
$tempPrcDir = Join-Path ([System.IO.Path]::GetTempPath()) ("toontown-bam2egg-prc-" + [guid]::NewGuid().ToString("N"))
Ensure-Path $tempPrcDir
$prcPath = Join-Path $tempPrcDir "Config.prc"
$externalRootPrcPath = Convert-ToPrcPath -Path $ExternalRoot
$resourcesRootPrcPath = Convert-ToPrcPath -Path $DestinationResourcesRoot
$prcContent = @(
  "model-path $externalRootPrcPath"
  "model-path $resourcesRootPrcPath"
) -join [Environment]::NewLine
Set-Content -Path $prcPath -Value $prcContent -NoNewline

$originalPandaPrcDir = $env:PANDA_PRC_DIR
$env:PANDA_PRC_DIR = $tempPrcDir

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
$fallbackEggAfterBamFailure = 0
$missing = New-Object System.Collections.Generic.List[string]
$alreadyPresent = 0
$texturelessEggs = New-Object System.Collections.Generic.List[string]
$manifestRows = New-Object System.Collections.Generic.List[object]
$phases = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)

try {
  foreach ($modelPath in ($modelPaths | Sort-Object)) {
    if ([string]::IsNullOrWhiteSpace($modelPath)) {
      continue
    }

    $normalizedModelPath = $modelPath.Replace('\', '/').TrimStart('/')
    $sourceEgg = Join-Path $ExternalRoot ($normalizedModelPath + ".egg")
    $sourceBam = Join-Path $ExternalRoot ($normalizedModelPath + ".bam")
    $targetEgg = Join-Path $DestinationResourcesRoot ($normalizedModelPath + ".egg")

    $slashIdx = $normalizedModelPath.IndexOf('/')
    if ($slashIdx -gt 0) {
      $firstSegment = $normalizedModelPath.Substring(0, $slashIdx)
      if ($firstSegment -like "phase_*") {
        [void]$phases.Add($firstSegment)
      }
    }

    Ensure-Path (Split-Path -Parent $targetEgg)

    if ((Test-Path $targetEgg) -and -not $Force.IsPresent) {
      $alreadyPresent++
      continue
    }

    $hasSourceEgg = Test-Path $sourceEgg
    $hasSourceBam = Test-Path $sourceBam
    $selectedSource = $null
    if ($SourcePreference -eq "Bam") {
      if ($hasSourceBam) {
        $selectedSource = "bam"
      }
      elseif ($hasSourceEgg) {
        $selectedSource = "egg"
      }
    }
    else {
      if ($hasSourceEgg) {
        $selectedSource = "egg"
      }
      elseif ($hasSourceBam) {
        $selectedSource = "bam"
      }
    }

    if ($selectedSource -eq "bam") {
      & bam2egg -o $targetEgg $sourceBam
      if ($LASTEXITCODE -ne 0) {
        if ($hasSourceEgg) {
          Copy-Item -LiteralPath $sourceEgg -Destination $targetEgg -Force
          $copiedEgg++
          $fallbackEggAfterBamFailure++
        }
        else {
          $missing.Add("$normalizedModelPath (bam2egg failed)")
          continue
        }
      }
      else {
        $converted++
      }
    }
    elseif ($selectedSource -eq "egg") {
      Copy-Item -LiteralPath $sourceEgg -Destination $targetEgg -Force
      $copiedEgg++
    }
    else {
      $missing.Add("$normalizedModelPath (no .egg or .bam)")
      continue
    }

    $textureStats = Get-EggTextureStats -EggPath $targetEgg
    foreach ($texturePhase in $textureStats.MapPhases) {
      [void]$phases.Add($texturePhase)
    }

    if ($textureStats.TextureDefinitions -eq 0) {
      $texturelessEggs.Add($normalizedModelPath)
    }

    $manifestRows.Add([PSCustomObject]@{
      modelPath = $normalizedModelPath
      source = $selectedSource
      outputEgg = $targetEgg.Replace('\', '/')
      textureDefinitions = $textureStats.TextureDefinitions
      textureReferences = $textureStats.TextureReferences
    })
  }
}
finally {
  if ($null -eq $originalPandaPrcDir) {
    Remove-Item Env:PANDA_PRC_DIR -ErrorAction SilentlyContinue
  }
  else {
    $env:PANDA_PRC_DIR = $originalPandaPrcDir
  }

  Remove-Item -LiteralPath $tempPrcDir -Recurse -Force -ErrorAction SilentlyContinue
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
Write-Host ("Egg fallback after bam: {0}" -f $fallbackEggAfterBamFailure) -ForegroundColor Green
Write-Host ("Already present:        {0}" -f $alreadyPresent) -ForegroundColor Green
Write-Host ("Missing/failed:         {0}" -f $missing.Count) -ForegroundColor Yellow
Write-Host ("Textureless .egg files: {0}" -f $texturelessEggs.Count) -ForegroundColor Yellow

if ($missing.Count -gt 0) {
  Write-Host "First missing/failed entries:" -ForegroundColor Yellow
  $missing | Select-Object -First 20 | ForEach-Object { Write-Host ("- " + $_) -ForegroundColor Yellow }
}

if ($texturelessEggs.Count -gt 0) {
  Write-Host "First textureless .egg entries:" -ForegroundColor Yellow
  $texturelessEggs | Select-Object -First 20 | ForEach-Object { Write-Host ("- " + $_) -ForegroundColor Yellow }
}

if ($WriteManifest) {
  $manifestDirectory = Split-Path -Parent $ManifestPath
  if (-not [string]::IsNullOrWhiteSpace($manifestDirectory)) {
    Ensure-Path $manifestDirectory
  }

  $manifest = [PSCustomObject]@{
    generatedAtUtc = [DateTime]::UtcNow.ToString("o")
    sourcePreference = $SourcePreference
    convertedCount = $converted
    copiedEggCount = $copiedEgg
    fallbackEggAfterBamFailureCount = $fallbackEggAfterBamFailure
    alreadyPresentCount = $alreadyPresent
    missingCount = $missing.Count
    texturelessEggCount = $texturelessEggs.Count
    entries = $manifestRows
  }

  $manifest | ConvertTo-Json -Depth 6 | Set-Content -Path $ManifestPath
  Write-Host ("Wrote manifest: {0}" -f $ManifestPath) -ForegroundColor Green
}

if ($FailOnTexturelessEgg.IsPresent -and $texturelessEggs.Count -gt 0) {
  throw ("Converted/copied files without <Texture> blocks: {0}" -f $texturelessEggs.Count)
}

Write-Host "Toontown DNA asset import prep complete." -ForegroundColor Cyan
