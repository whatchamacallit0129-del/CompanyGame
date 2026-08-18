# Rerun Plan

## Goal
Build the Unity 2D company-management game with soft-coded, extensible corridor/node navigation and employee systems.

## Current task
- task_id: employee-movement-foundation
- Integrate reusable employee selection -> destination -> node pathfinding -> movement without employee-specific hardcoding.

## Dependencies
- Corridor/path-node graph and explicit connections.
- CompanyGameNavigationGraph and CompanyGameNavigationService.
- CompanyGameEmployeeMovement.
- CompanyGameEmployeeSelectionController.
- Employee identity/creation pipeline.
- Unity Input System.

## Current implementation
- Selection controller exists in `Assets/Scripts/Movement/CompanyGameEmployeeSelectionController.cs` and uses Unity Input System mouse input.
- Selection controller supports single selection, drag selection, and right-click movement commands.
- EmployeeMovement requests paths from the navigation graph/service and follows returned nodes.
- Navigation graph/service are independent of corridor-specific types.
- `CompanyGameEmployeeMovementBootstrap` automatically attaches the reusable movement component to runtime `EmployeeId` objects.

## Reconciliation findings
- The required movement/selection/pathfinding source files are present on `main`.
- The selection controller was verified to call `CompanyGameEmployeeMovement.MoveTo()` for selected employees.
- The movement component was verified to consume `CompanyGameNavigationService` and safely stop/log when no route exists.
- Runtime verification cannot be performed through the connected GitHub interface; Unity Play Mode is required for the remaining acceptance criteria.

## Acceptance
1. No employee-specific hard-coded movement logic.
2. Selected employees can receive destinations and route through reachable nodes.
3. Movement follows the returned path and stops at the destination.
4. Unreachable destinations fail safely with diagnostics.
5. Design remains extensible for multiple employees, floors, and future rules.

## Verification
Source-level reconciliation is complete. Remaining verification must be performed in Unity: compile, enter Play Mode, select one employee, right-click a reachable connected corridor/node destination, verify movement, then test an unreachable destination and capture the Unity Console diagnostic. Do not mark complete from source inspection alone.
