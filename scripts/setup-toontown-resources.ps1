param(
  [string]$TargetDir = "External/open-toontown-resources",
  [string]$RepoUrl = "https://github.com/open-toontown/resources.git",
  [switch]$PullLatest
)

$ErrorActionPreference = 'Stop'

Write-Host "=== Setup Toontown Resources ===" -ForegroundColor Cyan

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
  throw "git is required but was not found in PATH."
}

$targetFullPath = [System.IO.Path]::GetFullPath($TargetDir)
$targetGitPath = Join-Path $targetFullPath ".git"

if (-not (Test-Path $targetGitPath)) {
  Write-Host "Cloning OpenToontown resources into $targetFullPath" -ForegroundColor Yellow
  & git clone --filter=blob:none --sparse $RepoUrl $targetFullPath
  if ($LASTEXITCODE -ne 0) {
    throw "git clone failed with exit code $LASTEXITCODE"
  }
}
else {
  Write-Host "Resources repo already present at $targetFullPath" -ForegroundColor Green
}

Write-Host "Configuring sparse checkout (phase_3, phase_3.5, phase_4, phase_5, models)..." -ForegroundColor Yellow
& git -C $targetFullPath sparse-checkout set phase_3 phase_3.5 phase_4 phase_5 models
if ($LASTEXITCODE -ne 0) {
  throw "sparse-checkout configuration failed with exit code $LASTEXITCODE"
}

if ($PullLatest.IsPresent) {
  Write-Host "Pulling latest sparse content..." -ForegroundColor Yellow
  & git -C $targetFullPath pull --ff-only
  if ($LASTEXITCODE -ne 0) {
    throw "git pull failed with exit code $LASTEXITCODE"
  }
}

$samplePath = Join-Path $targetFullPath "phase_4/dna/toontown_central_sz.dna"
if (Test-Path $samplePath) {
  Write-Host "Sample DNA ready: $samplePath" -ForegroundColor Green
}
else {
  Write-Host "Setup completed, but sample DNA was not found at expected path: $samplePath" -ForegroundColor Yellow
}

Write-Host "Toontown resources setup complete." -ForegroundColor Green
