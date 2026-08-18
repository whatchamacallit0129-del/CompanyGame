# Rerun State

- run_id: rerun-20260819-companygame-8f2c1d7a
- sequence: 0
- task_id: employee-movement-foundation
- status: needs_user
- checkpoint: Reconciled the current `main` source without repeating verified work. Movement, selection, navigation graph/service, and runtime movement bootstrap are present. Selection uses Unity Input System and calls `MoveTo`; movement consumes the node path service and fails safely when no route exists.
- verification: GitHub source inspection confirms `CompanyGameEmployeeSelectionController.cs`, `CompanyGameEmployeeMovement.cs`, `CompanyGameEmployeeMovementBootstrap.cs`, `CompanyGameNavigationGraph.cs`, and `CompanyGameNavigationService.cs` are present on `main`. Runtime Play Mode behavior remains unverified because the connected GitHub interface cannot run Unity or observe the Unity Console/Scene.
- next_exact_action: In Unity, compile and enter Play Mode. Select one employee with left click, right-click a reachable connected corridor/node destination, verify the employee follows the node path and stops at the destination. Then right-click an unreachable destination and capture the Unity Console diagnostic. Report the observed result before further source changes.
