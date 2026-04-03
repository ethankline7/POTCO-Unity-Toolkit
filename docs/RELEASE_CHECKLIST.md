# Release Checklist

Use this checklist before publishing major Toontown migration milestones.

## 1. Branch and Scope
- [ ] Branch is focused on one milestone objective.
- [ ] Commit history is clear and logically grouped.
- [ ] No unrelated files are included.

## 2. Local Validation
- [ ] Run primary checks:
  - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/primary-checks.ps1`
- [ ] Run manual readiness:
  - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/manual-readiness.ps1 -BaseRef upstream/main`
- [ ] Confirm Unity tool windows open and function for changed paths.

## 3. Documentation
- [ ] `docs/TOONTOWN_MIGRATION_PLAN.md` updated with behavior changes.
- [ ] `docs/BEGINNER_REMINDERS.md` updated when new pitfalls are found.
- [ ] `README.md` links are still accurate for contributor flow.

## 4. Pull Request Quality
- [ ] Pull request template is completed.
- [ ] `Primary CI` is green.
- [ ] `PR Readiness` is green.
- [ ] Reviewer can explain the change intent in one sentence.

## 5. Post-Merge Follow-Up
- [ ] Confirm `Weekly Health` run remains green after merge.
- [ ] Capture next small milestone in migration plan.
- [ ] Document any temporary compromises and cleanup plan.
