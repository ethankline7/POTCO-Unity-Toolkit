param(
  [string]$UnityExePath,
  [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
  [string]$LogPath = "Temp/toontown-dna-mvp-demo.log",
  [switch]$SkipResourceSetup,
  [switch]$AllowEditorVersionMismatch
)

$ErrorActionPreference = 'Stop'

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

  # Prefer project-pinned/recommended editors before trying newest installed versions.
  Add-Candidate "C:\Program Files\Unity\Hub\Editor\6000.1.11f1\Editor\Unity.exe"
  Add-Candidate "C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe"
  Add-Candidate "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Unity.exe"

  $hubEditorsRoot = "C:\Program Files\Unity\Hub\Editor"
  if (Test-Path $hubEditorsRoot) {
    $versionDirs = Get-ChildItem -Path $hubEditorsRoot -Directory | Sort-Object Name -Descending
    foreach ($dir in $versionDirs) {
      $candidate = Join-Path $dir.FullName "Editor\Unity.exe"
      Add-Candidate $candidate
    }
  }

  foreach ($candidate in $candidates) {
    if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path $candidate)) {
      return (Resolve-Path $candidate).Path
    }
  }

  return $null
}

function Get-ProjectEditorVersion {
  param([string]$ProjectRoot)

  $projectVersionPath = Join-Path $ProjectRoot "ProjectSettings\ProjectVersion.txt"
  if (-not (Test-Path $projectVersionPath)) {
    return $null
  }

  $match = Select-String -Path $projectVersionPath -Pattern '^m_EditorVersion:\s*(.+)$' | Select-Object -First 1
  if (-not $match) {
    return $null
  }

  return $match.Matches[0].Groups[1].Value.Trim()
}

function Get-UnityEditorVersionFromPath {
  param([string]$UnityPath)

  if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    return $null
  }

  $editorDirectory = Split-Path -Parent $UnityPath
  $versionDirectory = Split-Path -Parent $editorDirectory
  return Split-Path -Leaf $versionDirectory
}

function Assert-UnityEditorMatchesProject {
  param(
    [string]$ProjectRoot,
    [string]$UnityPath
  )

  if ($AllowEditorVersionMismatch.IsPresent) {
    return
  }

  $projectEditorVersion = Get-ProjectEditorVersion -ProjectRoot $ProjectRoot
  $unityEditorVersion = Get-UnityEditorVersionFromPath -UnityPath $UnityPath
  if ([string]::IsNullOrWhiteSpace($projectEditorVersion) -or
      [string]::IsNullOrWhiteSpace($unityEditorVersion) -or
      $projectEditorVersion -eq $unityEditorVersion) {
    return
  }

  throw "Unity editor version mismatch. Project is pinned to $projectEditorVersion, but resolved editor is $unityEditorVersion. Pass -UnityExePath for the pinned editor, or pass -AllowEditorVersionMismatch only when intentionally validating the Unity upgrade lane."
}

$projectFullPath = (Resolve-Path $ProjectPath).Path
$unityPath = Resolve-UnityEditorPath -PreferredPath $UnityExePath
if ([string]::IsNullOrWhiteSpace($unityPath)) {
  throw "Unity editor executable not found. Pass -UnityExePath or set UNITY_EDITOR_PATH."
}

Assert-UnityEditorMatchesProject -ProjectRoot $projectFullPath -UnityPath $unityPath

if (-not $SkipResourceSetup.IsPresent) {
  & "$PSScriptRoot/setup-toontown-resources.ps1"
}

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
  "-executeMethod", "Toontown.Editor.Validation.ToontownDnaMvpDemoRunner.RunBatch",
  "-logFile", $logFullPath
)

Write-Host "=== Toontown DNA MVP Demo (Batch) ===" -ForegroundColor Cyan
Write-Host ("Unity: {0}" -f $unityPath) -ForegroundColor Yellow
Write-Host ("Project: {0}" -f $projectFullPath) -ForegroundColor Yellow
Write-Host ("Log: {0}" -f $logFullPath) -ForegroundColor Yellow

$process = Start-Process -FilePath $unityPath -ArgumentList $args -Wait -PassThru

if ($process.ExitCode -eq 0) {
  Write-Host "DNA MVP demo import completed successfully." -ForegroundColor Green

  if (Test-Path $logFullPath) {
    $keyLines = Select-String -Path $logFullPath -Pattern "Toontown DNA MVP Demo Import|Status:|Parsed objects:|Document warnings:|Created scene objects:|Instantiated models:|Missing models:|Placeholders created:|Fake shadow renderers disabled:|Resolved-node isolate success:|Resolved-node isolate failed:|Door/window parent anchors:|Door/window parent anchor misses:|Forced EGG imports:|Warning categories:|- missing model:|- missing resolved node:|- fallback placement:|- material fallback:|- fake shadow removal:|- uncategorized document warning:|Output scene:"
    if ($keyLines) {
      Write-Host "Summary from log:" -ForegroundColor Cyan
      foreach ($line in $keyLines) {
        Write-Host $line.Line
      }
    }
  }

  exit 0
}

Write-Host ("DNA MVP demo import failed with exit code {0}." -f $process.ExitCode) -ForegroundColor Red
if (Test-Path $logFullPath) {
  Write-Host "Last 160 log lines:" -ForegroundColor Yellow
  Get-Content $logFullPath -Tail 160
}

exit $process.ExitCode
