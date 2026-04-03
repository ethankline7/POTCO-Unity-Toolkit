# Agent Collaboration Guide

This file defines how human contributors and coding agents should work in this repository.

## Core Rules
- Keep changes scoped to one objective per commit.
- Prefer additive scaffolding before risky refactors.
- Do not rewrite POTCO behavior when adding Toontown support unless explicitly planned.
- Document intent for non-trivial changes in `docs/TOONTOWN_MIGRATION_PLAN.md`.

## Branching and Commits
- Use feature branches prefixed with `codex/`.
- Write commit messages in imperative style.
- Keep commits reviewable (small and focused).

## Safety Checks Before Commit
- No unresolved merge conflict markers.
- No unrelated files included in the commit.
- No placeholder TODOs without context.

## Task Order for Migration
1. Shared interfaces and routing
2. POTCO adapters behind interfaces
3. Toontown adapters behind interfaces
4. Validation and cleanup

## Notes for Agents
- Respect existing style and directory boundaries.
- Do not fabricate authorship or hide generated work.
- Surface assumptions early when requirements are unclear.