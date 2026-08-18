# Rerun State

- run_id: rerun-20260819-companygame-8f2c1d7a
- sequence: 0
- task_id: employee-movement-foundation
- status: continue
- checkpoint: Resumed the active sequence after re-reading the Rerun contract and reconciling control/STATE/PLAN. Previously verified source work remains unchanged and is not being repeated.
- verification: GitHub source inspection remains complete for CompanyGameEmployeeSelectionController, CompanyGameEmployeeMovement, CompanyGameEmployeeMovementBootstrap, CompanyGameNavigationGraph, and CompanyGameNavigationService. Runtime Play Mode behavior is still unverified because the connected GitHub interface cannot run Unity or observe the Unity Console/Scene.
- next_exact_action: In Unity, compile and enter Play Mode. Select one employee with left click, right-click a reachable connected corridor/node destination, verify the employee follows the node path and stops at the destination. Then right-click an unreachable destination and capture the Unity Console diagnostic. Report the observed result before further source changes.
