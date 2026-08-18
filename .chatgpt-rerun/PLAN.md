# Rerun Plan

## Goal
Build the Unity 2D company-management game with soft-coded, extensible corridor/node navigation and employee systems.

## First task
- task_id: employee-movement-foundation
- Build the selection -> destination -> node pathfinding -> employee movement flow using reusable components/services.

## Dependencies
- Corridor/path-node graph and connections.
- CompanyGameNavigationGraph and path service.
- CompanyGameEmployeeMovement.
- Employee identity/creation pipeline.
- Unity Input System.

## Acceptance
1. No employee-specific hard-coded movement logic.
2. Selected employees can receive destinations and route through reachable nodes.
3. Movement follows the returned path and stops at the destination.
4. Unreachable destinations fail safely with diagnostics.
5. Design remains extensible for multiple employees, floors, and future rules.

## Verification
Compile in Unity, run a selection/destination movement test, and preserve evidence in STATE/STATUS before completion.
