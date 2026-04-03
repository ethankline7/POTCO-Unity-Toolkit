param(
  [string]$BaseRef = "origin/main"
)

$ErrorActionPreference = 'Stop'

Write-Host '=== Commit Message Check ===' -ForegroundColor Cyan
Write-Host ("Base reference: {0}" -f $BaseRef) -ForegroundColor Yellow

$pattern = '^(feat|fix|chore|docs|refactor|test|ci)(\([a-z0-9_\/-]+\))?: [A-Za-z0-9].+'

git rev-parse --verify $BaseRef *> $null
if ($LASTEXITCODE -ne 0) {
  throw "Base ref not found locally: $BaseRef"
}

$subjects = git log --format=%s "$BaseRef..HEAD"
if (-not $subjects) {
  Write-Host 'No commits found in range; skipping.' -ForegroundColor Yellow
  exit 0
}

$invalid = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

foreach ($subject in $subjects) {
  if ($subject.Length -gt 72) {
    $warnings.Add("Subject longer than 72 chars (recommended max): $subject")
  }

  if ($subject -notmatch $pattern) {
    $invalid.Add("Subject does not match required pattern: $subject")
  }
}

if ($invalid.Count -gt 0) {
  Write-Host 'Invalid commit subjects found:' -ForegroundColor Red
  foreach ($line in $invalid) {
    Write-Host ("- {0}" -f $line) -ForegroundColor Red
  }

  throw "Commit message check failed. Use: type(scope): summary"
}

if ($warnings.Count -gt 0) {
  Write-Host 'Commit message warnings:' -ForegroundColor Yellow
  foreach ($line in $warnings) {
    Write-Host ("- {0}" -f $line) -ForegroundColor Yellow
  }
}

Write-Host 'Commit message check: OK' -ForegroundColor Green
