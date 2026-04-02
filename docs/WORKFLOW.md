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
  - `Primary CI` for push and branch quality gates.
  - `Commit Quality` for commit subject format and readability checks.
  - `PR Readiness` for pull-request base diff sanity and release-style checks.
  - `Manual Readiness` for operator-triggered release sanity checks.
  - `Weekly Health` for scheduled baseline checks on the default branch.
- Primary checks include a guard that Toontown reader/writer are not reverted to stub-only implementations.
- Primary checks validate Toontown type-map config JSON (`Assets/Editor/Toontown/Config/ObjectTypeMap.json`).
- Primary checks validate Toontown bundled sample sanity (`scripts/toontown-sample-sanity.ps1`).
- For Toontown tuning work, use `Toontown/Validation/Sample Validator` before changing type-map rules.

### Action Quick Reference
| Action | Trigger | What it validates |
| --- | --- | --- |
| `Primary CI` | Push, pull request, manual dispatch | Required docs, merge-conflict markers, Toontown adapter guardrails, type-map JSON validity |
| `Commit Quality` | Push, pull request, manual dispatch | Commit subject style (`type(scope): summary`) with a soft warning for subjects over 72 chars |
| `PR Readiness` | Pull request, manual dispatch | Primary checks plus branch visibility, recent commit trail, and diff summary against PR base |
| `Manual Readiness` | Manual dispatch | Operator-controlled readiness pass against a chosen base reference |
| `Weekly Health` | Weekly schedule, manual dispatch | Recurring baseline `primary-checks` run for drift detection |

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

## Release Gate
- Use `docs/RELEASE_CHECKLIST.md` before milestone merges.
