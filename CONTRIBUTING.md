# Contributing

This repository is run with a human-led workflow and small, reviewable commits.

## Quick Start
1. Fork this repository in GitHub.
2. Clone your fork locally.
3. Add upstream:
   - `git remote add upstream https://github.com/JackScurvy/POTCO-Unity-Toolkit.git`
4. Create a feature branch:
   - `git checkout -b codex/<short-feature-name>`

## Git Identity (GitHub private email)
Use GitHub's private noreply email for local commits:
- `git config user.name "sirwranwrap"`
- `git config user.email "sirwranwrap@users.noreply.github.com"`

## Commit Rules
- One logical objective per commit.
- Use imperative commit messages:
  - `feat: add Toontown sample validator CSV export`
  - `chore: add PR readiness run action`
- Do not mix broad refactors with behavior changes in one commit.

## Required Checks Before Commit
- `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/primary-checks.ps1`
- Optional release-style pass:
  - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/manual-readiness.ps1 -BaseRef upstream/main`

## Pull Request Steps
1. Push your branch:
   - `git push -u origin codex/<short-feature-name>`
2. Open a pull request to `main`.
3. Complete `.github/pull_request_template.md`.
4. Ensure Actions are green:
   - `Primary CI`
   - `PR Readiness`

## Beginner Steering
Common mistakes to avoid:
- editing many systems before a first commit
- skipping primary checks
- changing POTCO behavior while intending Toontown-only scaffolding

If you are unsure, update `docs/TOONTOWN_MIGRATION_PLAN.md` first, then implement in small steps.
