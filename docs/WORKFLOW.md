# Engineering Workflow

## Human-Led Authorship Standard
- You make final decisions on scope and direction.
- Every commit message is written as an intentional engineering decision.
- Major changes include a short note in docs explaining why the change exists.

## Branching
- Use feature branches with prefix `codex/`.
- Keep one purpose per branch.
- Avoid mixing refactors and features in the same commit.

## Commit Standards
- Use clear commit subjects in imperative mood.
- Keep commits scoped to one logical unit.
- Commit order:
  1. docs/chore
  2. scaffolding
  3. behavior changes
  4. cleanup

## Run Actions
- Primary (recommended): run `scripts/primary-checks.ps1`.
- Manual readiness bundle: run `scripts/manual-readiness.ps1`.
- CI mirrors:
  - `Primary CI` for push/PR/default checks.
  - `Manual Readiness` for operator-triggered release sanity checks.

## Quality Gates
- No dead code in committed changes.
- Keep all new files and symbols named by domain intent.
- Keep editor-only code under `Assets/Editor`.
- Keep runtime code under `Assets/Scripts`.
- Run quick grep checks before committing:
  - no accidental TODO placeholders without context
  - no cross-domain hardcoding in shared layers

## Steering Checkpoints
- Checkpoint A (before coding): confirm exact objective in one sentence.
- Checkpoint B (before commit): confirm only one logical unit changed.
- Checkpoint C (after commit): confirm next action is clear and small.

## Review Checklist
- Is change scoped and reversible?
- Is naming clear to another engineer?
- Is behavior change documented?
- Is there an obvious next step for the following commit?
