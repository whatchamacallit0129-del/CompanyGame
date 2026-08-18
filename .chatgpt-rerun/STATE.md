# Rerun State

- run_id: rerun-20260819-companygame-8f2c1d7a
- sequence: 0
- task_id: employee-movement-foundation
- status: continue
- checkpoint: Reconciled current repository state. Existing selection, navigation graph/service, and employee movement components are present. The missing integration identified during reconciliation was automatic attachment of the movement capability to runtime employees; `CompanyGameEmployeeMovementBootstrap` was added.
- verification: GitHub source inspection confirms `CompanyGameEmployeeSelectionController` uses Unity Input System and issues `MoveTo` to selected employees. `CompanyGameEmployeeMovement` consumes `CompanyGameNavigationService`. Navigation graph/service are corridor-agnostic. Bootstrap commit `e8700e519dfde2d30fb1c2f762e77c1f5991a67d` adds movement agents to every runtime EmployeeId object.
- next_exact_action: Compile in Unity and run Play Mode verification with connected corridor nodes: select one employee, right-click a reachable corridor/node destination, verify movement; then test an unreachable destination and capture diagnostics.
