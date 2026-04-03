param()

$ErrorActionPreference = 'Stop'

Write-Host '=== Project Checks ===' -ForegroundColor Cyan

# 1) Ensure required docs exist
$required = @(
  'AGENTS.md',
  'docs/WORKFLOW.md',
  'docs/TOONTOWN_MIGRATION_PLAN.md',
  'docs/BEGINNER_REMINDERS.md',
  'docs/TOONTOWN_QUICKSTART.md'
)

foreach ($path in $required) {
  if (-not (Test-Path $path)) {
    throw "Missing required file: $path"
  }
}
Write-Host 'Required docs: OK' -ForegroundColor Green

# 2) Ensure no merge conflict markers in tracked files
# Match only true conflict marker lines, not divider comments with equals.
$conflicts = git grep -n -E "^(<<<<<<< .+|=======|>>>>>>> .+)$" -- . ':!*.png' ':!*.jpg' ':!*.jpeg' ':!*.meta' 2>$null
if ($LASTEXITCODE -eq 0 -and $conflicts) {
  Write-Host $conflicts
  throw 'Merge conflict markers found.'
}
Write-Host 'Conflict markers: OK' -ForegroundColor Green

# 3) Show short git state for operator awareness
$branch = git branch --show-current
Write-Host ("Current branch: {0}" -f $branch) -ForegroundColor Yellow

git status --short

Write-Host 'All checks passed.' -ForegroundColor Green
