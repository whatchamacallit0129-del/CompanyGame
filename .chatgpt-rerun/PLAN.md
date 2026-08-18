# Rerun Plan

## Goal
Build the Unity 2D company-management game with soft-coded, extensible corridor/node navigation and employee systems, while protecting verified functionality from autonomous regressions.

## Current task
- task_id: architecture-audit
- First inspect and document the current architecture before any gameplay implementation changes.

## Dependencies
- Corridor/path-node graph and explicit connections.
- CompanyGameNavigationGraph and CompanyGameNavigationService.
- CompanyGameEmployeeMovement.
- CompanyGameEmployeeSelectionController.
- Employee identity/creation pipeline.
- Unity Input System.
- Command Agent / Auto Pull pipeline.

## Audit scope
- Map major Unity systems, their responsibilities, references, and data flow.
- Identify hard-coded coupling, duplicated responsibilities, unsafe automation points, and missing validation boundaries.
- Record which systems are already source-verified so Rerun does not recreate them.
- Recommend small implementation tasks with explicit dependencies and acceptance criteria.

## Safety constraints
- Do not modify gameplay code during `architecture-audit`.
- Do not delete or rename existing gameplay systems.
- Do not change Unity project settings.
- Do not alter verified movement/navigation behavior merely for cleanup.
- A backup snapshot exists at `.chatgpt-rerun/BACKUP-20260819-seq0.md` before this workflow change.

## Known verified implementation
- Selection controller exists and uses Unity Input System mouse input.
- Selection controller supports single selection, drag selection, and right-click movement commands.
- EmployeeMovement requests paths from the navigation graph/service and follows returned nodes.
- Navigation graph/service are independent of corridor-specific types.
- EmployeeMovementBootstrap automatically attaches the reusable movement component to runtime EmployeeId objects.
- Runtime Play Mode behavior remains unverified through GitHub.

## Acceptance criteria for architecture-audit
1. Produce an architecture map of the current project.
2. Identify dependencies and risky coupling without changing gameplay code.
3. Separate verified facts from assumptions.
4. Produce a prioritized list of the next safe implementation/verification tasks.
5. Leave existing gameplay functionality untouched.

## Verification method
Use GitHub source inspection for the audit. Do not claim runtime success from source inspection. Runtime acceptance remains a separate Unity Play Mode task after the audit is reviewed/authorized.
