param(
  [string]$BaseRef = "upstream/main"
)

$ErrorActionPreference = 'Stop'

Write-Host '=== Manual Readiness ===' -ForegroundColor Cyan
Write-Host ("Base reference: {0}" -f $BaseRef) -ForegroundColor Yellow

# 1) Run primary checks.
& "$PSScriptRoot/primary-checks.ps1"

# 2) Show branch and commit visibility.
$branch = git branch --show-current
Write-Host ("Current branch: {0}" -f $branch) -ForegroundColor Yellow
Write-Host 'Recent commits:' -ForegroundColor Cyan
git log --oneline --decorate -n 12

# 3) Show delta summary from base ref if available.
git rev-parse --verify $BaseRef *> $null
if ($LASTEXITCODE -eq 0) {
  $range = "{0}...HEAD" -f $BaseRef
  Write-Host ("Diff summary vs {0}:" -f $BaseRef) -ForegroundColor Cyan
  git diff --stat "$range"
  if ($LASTEXITCODE -ne 0) {
    Write-Host ("Could not compute diff summary for range: {0}" -f $range) -ForegroundColor Yellow
  }
} else {
  Write-Host ("Base ref not found locally: {0}" -f $BaseRef) -ForegroundColor Yellow
}

Write-Host 'Manual readiness checks completed.' -ForegroundColor Green
