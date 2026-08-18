# ChatGPT Rerun Project Contract

## Mandatory read order
README.md -> .chatgpt-rerun/control.json -> STATE.md -> PLAN.md

## Preflight
Reconcile control, STATE, PLAN, STATUS, and repository instructions before work. Preserve an active run_id and verification history. Never reset or overwrite an active run's completed verification records. Before any code change, confirm the current task, dependencies, acceptance criteria, and next exact action.

## Safety / audit-first mode
Rerun must not begin by changing game code. The first development task is an architecture audit. Read the project structure and relevant source, document responsibilities and dependencies, and identify risks without modifying gameplay code. Only after the user reviews/authorizes the audit may implementation tasks begin.

## Execution
20-minute hard stop; checkpoint at about 18 minutes. For long active work, target STATUS.md freshness about every 5 minutes. Prefer small, independently verifiable tasks. Do not repeat work already marked verified.

## Authoritative writes
PLAN.md -> STATE.md -> control.json. STATUS.md is a human-readable projection and is not the reconciliation source of truth. Update STATUS.md immediately on meaningful state changes and target about 5-minute freshness during long active execution.

## Control
Use `continue` for work start/resume. `complete`, `needs_user`, and `blocked` are dispatch wait states and must not stop watcher polling. The same sequence may resume after a terminal state when a new `continue` authorization arrives. Never use `working`.

## Side Panel
Chrome Side Panel Start/Stop controls the tab watcher only and is independent of GitHub control status. Start/Stop must not be interpreted as changing control.json state.

## Project
Unity 2D company-management simulation inspired by Lobotomy Corporation. The user wants soft-coded, extensible corridor/node navigation, employee selection, pathfinding, and movement, with later systems built on reusable foundations.

## Change safety
Before risky source changes, create or update a clearly named backup snapshot. Do not delete or overwrite verified project functionality merely to satisfy a new task. Prefer additive, modular changes and preserve compatibility with existing command/Unity pipelines.

## First-task policy
The first Rerun task is `architecture-audit`: inspect the current project and produce an architecture map, dependency/risk list, and recommended next tasks. No gameplay code changes are allowed in this task. The audit must be reviewed before moving to implementation.
