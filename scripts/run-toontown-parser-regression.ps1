param(
  [string]$UnityExePath,
  [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
  [string]$LogPath = "Temp/toontown-parser-regression.log",
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
  "-executeMethod", "Toontown.Editor.Validation.ToontownParserRegressionRunner.RunBatch",
  "-logFile", $logFullPath
)

Write-Host "=== Toontown Parser Regression (Batch) ===" -ForegroundColor Cyan
Write-Host ("Unity: {0}" -f $unityPath) -ForegroundColor Yellow
Write-Host ("Project: {0}" -f $projectFullPath) -ForegroundColor Yellow
Write-Host ("Log: {0}" -f $logFullPath) -ForegroundColor Yellow

$process = Start-Process -FilePath $unityPath -ArgumentList $args -Wait -PassThru

if ($process.ExitCode -eq 0) {
  Write-Host "Toontown parser regression passed." -ForegroundColor Green

  if (Test-Path $logFullPath) {
    $keyLines = Select-String -Path $logFullPath -Pattern "Toontown Parser Regression|Status:|- \[PASS\]|- \[FAIL\]"
    if ($keyLines) {
      Write-Host "Summary from log:" -ForegroundColor Cyan
      foreach ($line in $keyLines) {
        Write-Host $line.Line
      }
    }
  }

  exit 0
}

Write-Host ("Toontown parser regression failed with exit code {0}." -f $process.ExitCode) -ForegroundColor Red
if (Test-Path $logFullPath) {
  Write-Host "Last 120 log lines:" -ForegroundColor Yellow
  Get-Content $logFullPath -Tail 120
}

exit $process.ExitCode
