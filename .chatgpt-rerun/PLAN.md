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
- Selection controller already supports single selection, drag selection, and right-click movement commands.
- EmployeeMovement already requests paths from the navigation graph/service and follows returned nodes.
- Navigation graph/service are independent of corridor-specific types.
- Added `CompanyGameEmployeeMovementBootstrap` so every runtime `EmployeeId` automatically receives the reusable movement component.

## Acceptance
1. No employee-specific hard-coded movement logic.
2. Selected employees can receive destinations and route through reachable nodes.
3. Movement follows the returned path and stops at the destination.
4. Unreachable destinations fail safely with diagnostics.
5. Design remains extensible for multiple employees, floors, and future rules.

## Verification
Unity compile and Play Mode verification are still required. Confirm that employees receive `CompanyGameEmployeeMovement`, selection works, a reachable right-click destination produces movement, and an unreachable destination logs a safe diagnostic. Preserve evidence in STATE/STATUS before completion.
