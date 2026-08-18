# ChatGPT Rerun Project Contract

## Mandatory read order
README.md -> .chatgpt-rerun/control.json -> STATE.md -> PLAN.md

## Preflight
Reconcile control, STATE, PLAN, and repository instructions before work. Preserve active run_id, sequence, task, and verification history; do not reset an active run.

## Execution
20-minute hard stop; checkpoint at about 18 minutes. For long active work, target STATUS.md freshness about every 5 minutes.

## Authoritative writes
PLAN.md -> STATE.md -> control.json. STATUS.md is a human-readable projection and is not the reconciliation source of truth. Update STATUS.md on meaningful state changes.

## Control
Use continue for work start/resume. complete, needs_user, and blocked are dispatch wait states and must not stop watcher polling. The same sequence may resume after a terminal state when a new continue authorization arrives. Never use working.

## Side Panel
Chrome Side Panel Start/Stop controls the tab watcher only and is independent of GitHub control status.

## Project
Unity 2D company-management simulation inspired by Lobotomy Corporation. The user wants soft-coded, extensible corridor/node navigation, employee selection, pathfinding, and movement. Existing repository code includes CompanyGameEmployeeMovement consuming a navigation graph/path service.
