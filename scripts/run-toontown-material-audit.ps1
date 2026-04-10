param(
  [string]$UnityExePath,
  [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
  [string]$LogPath = "Temp/toontown-material-audit.log",
  [int]$MaxMissingMainTex = -1,
  [switch]$AllowEditorVersionMismatch
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/unity-version-guard.ps1"

function Resolve-UnityEditorPath {
  param([string]$PreferredPath)

  $candidates = New-Object System.Collections.Generic.List[string]
  $seen = New-Object "System.Collections.Generic.HashSet[string]"

  function Add-Candidate {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) {
      return
    }

    if ($seen.Add($Path)) {
      $candidates.Add($Path)
    }
  }

  if (-not [string]::IsNullOrWhiteSpace($PreferredPath)) {
    Add-Candidate $PreferredPath
  }

  if (-not [string]::IsNullOrWhiteSpace($env:UNITY_EDITOR_PATH)) {
    Add-Candidate $env:UNITY_EDITOR_PATH
  }

  Add-Candidate "C:\Program Files\Unity\Hub\Editor\6000.1.11f1\Editor\Unity.exe"
  Add-Candidate "C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe"
  Add-Candidate "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Unity.exe"

  $hubEditorsRoot = "C:\Program Files\Unity\Hub\Editor"
  if (Test-Path $hubEditorsRoot) {
    $versionDirs = Get-ChildItem -Path $hubEditorsRoot -Directory | Sort-Object Name -Descending
    foreach ($dir in $versionDirs) {
      Add-Candidate (Join-Path $dir.FullName "Editor\Unity.exe")
    }
  }

  foreach ($candidate in $candidates) {
    if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path $candidate)) {
      return (Resolve-Path $candidate).Path
    }
  }

  return $null
}

function Get-AuditReportLines {
  param([string[]]$Lines)

  $startIndex = -1
  for ($i = 0; $i -lt $Lines.Count; $i++) {
    if ($Lines[$i].Contains("Toontown Scene Material Audit")) {
      $startIndex = $i
      break
    }
  }

  if ($startIndex -lt 0) {
    return @()
  }

  $reportLines = New-Object System.Collections.Generic.List[string]
  for ($i = $startIndex; $i -lt $Lines.Count; $i++) {
    $line = $Lines[$i]
    if ($i -gt $startIndex -and [string]::IsNullOrWhiteSpace($line)) {
      break
    }

    if ($i -gt $startIndex -and
        ($line.StartsWith("UnityEngine.") -or
         $line.StartsWith("(Filename:") -or
         $line.Contains("Toontown.Editor.Validation.ToontownSceneMaterialAuditRunner:"))) {
      break
    }

    $reportLines.Add($line)
    if ($reportLines.Count -ge 40) {
      break
    }
  }

  return $reportLines.ToArray()
}

$projectFullPath = (Resolve-Path $ProjectPath).Path
$unityPath = Resolve-UnityEditorPath -PreferredPath $UnityExePath
if ([string]::IsNullOrWhiteSpace($unityPath)) {
  throw "Unity editor executable not found. Pass -UnityExePath or set UNITY_EDITOR_PATH."
}

Assert-UnityEditorMatchesProject `
  -ProjectRoot $projectFullPath `
  -UnityPath $unityPath `
  -AllowMismatch:$AllowEditorVersionMismatch.IsPresent

$logFullPath = Join-Path $projectFullPath $LogPath
$logDirectory = Split-Path -Parent $logFullPath
if (-not (Test-Path $logDirectory)) {
  New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
}

$args = @(
  "-batchmode",
  "-nographics",
  "-quit",
  "-projectPath", $projectFullPath,
  "-executeMethod", "Toontown.Editor.Validation.ToontownSceneMaterialAuditRunner.RunBatch",
  "-logFile", $logFullPath
)

Write-Host "=== Toontown Scene Material Audit (Batch) ===" -ForegroundColor Cyan
Write-Host ("Unity: {0}" -f $unityPath) -ForegroundColor Yellow
Write-Host ("Project: {0}" -f $projectFullPath) -ForegroundColor Yellow
Write-Host ("Log: {0}" -f $logFullPath) -ForegroundColor Yellow

$process = Start-Process -FilePath $unityPath -ArgumentList $args -Wait -PassThru

if ($process.ExitCode -ne 0) {
  Write-Host ("Toontown material audit failed with exit code {0}." -f $process.ExitCode) -ForegroundColor Red
  if (Test-Path $logFullPath) {
    Write-Host "Last 120 log lines:" -ForegroundColor Yellow
    Get-Content $logFullPath -Tail 120
  }

  exit $process.ExitCode
}

$missingMainTex = $null
if (Test-Path $logFullPath) {
  $logLines = Get-Content $logFullPath
  $reportLines = Get-AuditReportLines -Lines $logLines
  if ($reportLines.Count -gt 0) {
    Write-Host "Summary from log:" -ForegroundColor Cyan
    foreach ($line in $reportLines) {
      Write-Host $line
      if ($line -match "Materials missing _MainTex:\s*(\d+)") {
        $missingMainTex = [int]$Matches[1]
      }
    }
  }
}

if ($MaxMissingMainTex -ge 0) {
  if ($null -eq $missingMainTex) {
    Write-Host "Could not parse 'Materials missing _MainTex' from material audit log." -ForegroundColor Red
    exit 1
  }

  if ($missingMainTex -gt $MaxMissingMainTex) {
    Write-Host ("Material audit exceeded threshold: missing _MainTex {0} > {1}." -f $missingMainTex, $MaxMissingMainTex) -ForegroundColor Red
    exit 1
  }
}

if ($null -ne $missingMainTex -and $missingMainTex -gt 0) {
  Write-Host ("Material audit completed with current missing _MainTex baseline: {0}." -f $missingMainTex) -ForegroundColor Yellow
}
else {
  Write-Host "Material audit completed successfully." -ForegroundColor Green
}

exit 0
