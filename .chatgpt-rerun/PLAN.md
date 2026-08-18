# Rerun Plan

## Overall Project Goal
Build the Unity 2D company-management simulation as a soft-coded, modular, extensible system. Preserve the existing roadmap: company management, corridor/process infrastructure, employees, production systems, day progression, policies, events, and later world/story systems.

## Current Development Focus
The immediate focus is **Corridor/Node infrastructure + Employee Movement**. These foundations must become reliable, understandable, visible, and testable before later gameplay systems.

## Architecture Audit
Completed in `ARCHITECTURE.md`. The audit confirms the existing separation between corridor/node authoring, shared navigation, employee movement, and employee selection. Existing verified movement/navigation source was preserved.

## Task A — Corridor / Node System
Build and refine reusable corridor and navigation-node infrastructure.
- Corridors are reusable scene components, not one-off hard-coded objects.
- Nodes belong to corridors and can be created/edited through reusable tooling.
- Corridor connection should be simple: select corridor, enter edit mode, select another corridor.
- Unconnected/available nodes are visually blue; connected nodes are visually green.
- Connections must create real navigation data, not only editor visuals.
- Navigation must not depend on a specific corridor implementation.
- Support multiple corridors, branches, junctions, and future floors.
- Keep corridors/nodes visible with useful editor visualization/toggles.
- Do not require matching manually placed nodes merely to connect corridors.

Acceptance criteria:
1. Multiple corridors remain visible simultaneously.
2. Nodes can be added to any corridor through reusable tooling.
3. Corridors can be connected through the intended edit-mode interaction.
4. Connection state is obvious.
5. Navigation queries the resulting shared graph without corridor-specific assumptions.
6. Existing verified corridor/node functionality is preserved unless a concrete bug requires change.

## Task B — Employee Movement System
Build reusable employee movement inspired by the **interaction model and practical feel of Lobotomy Corporation**, without copying proprietary code or assets.
- Select one employee by clicking them.
- Selected employees have clear visual feedback.
- Click a valid corridor/node destination to issue a movement order.
- Employee finds a path through the connected Node graph and follows it automatically.
- Branches and future multi-floor navigation must remain possible.
- Invalid/unreachable destinations fail gracefully with understandable feedback.
- Player should not manually manipulate nodes for routine employee movement.
- Multiple employees must use the same navigation system independently.

Keep these responsibilities separate:
- employee selection
- movement
- navigation/pathfinding
- corridor/editor tooling
- employee identity/data
- input handling

Soft-coded movement configuration should cover speed, stopping distance, path tolerance, destination validation, future priorities/queues, animation hooks, blocked/unreachable behavior, and floor transitions.

Acceptance criteria:
1. Runtime employee can be selected.
2. Destination can be selected through player interaction.
3. Shared Node graph produces the path.
4. Employee visibly follows the path.
5. Multiple employees work independently.
6. No employee-specific movement code is required.
7. Invalid/unreachable requests provide clear feedback.
8. Architecture remains extensible for work/production/AI.

## Testing / Iteration Loop
When an error appears:
1. Inspect actual source and result/error information.
2. Identify root cause.
3. Make the smallest modular fix that preserves behavior.
4. Re-check dependent systems.
5. Record verified and unverified results.

Do not claim Unity runtime success from GitHub inspection alone.

## Visibility / Usability
Prioritize readable editor/runtime feedback. Selected objects, nodes, connections, destinations, and movement states should be obvious at a glance. Prefer simple interactions over technically sophisticated workflows when both work.

## Soft-Coding Rules
- Avoid hard-coded employee names, instance IDs, scene-specific coordinates, and one-off object references.
- Prefer reusable components, interfaces/services, serialized configuration, data assets, registries, and graph queries.
- Depend on abstractions rather than concrete corridor instances.
- Preserve compatibility with Command Agent and Auto Pull.
- Do not modify the Rerun extension/program itself.

## Overall Roadmap After Movement Foundation
1. Employee work/assignment system.
2. Production/process infrastructure.
3. Resource/material flow.
4. Company management and finances.
5. Day-end/day-start progression and save opportunities.
6. Policies and decision windows.
7. Events, investigations, and reputation.
8. Story/worldbuilding progression and ethical dilemma systems.
9. Additional departments/floors/process chains.

## Safety Constraints
- Do not modify the Rerun extension/program itself unless explicitly requested.
- Do not delete or rename verified gameplay systems merely for cleanup.
- Do not change Unity project settings unless required and justified.
- Before risky gameplay source changes, create a clearly named backup snapshot outside the Rerun extension.
- Prefer additive, modular changes.
- Never use `working` control status.

## Existing Backup
`.chatgpt-rerun/BACKUP-20260819-seq0.md` is the historical pre-workflow-change snapshot. Preserve it. Additional backups must have unique names.

## Current Task
`corridor-node-authoring`

## Current Acceptance Focus
Verify/refine reusable corridor/node authoring and connection workflow, preserving existing navigation data and preparing reliable runtime movement tests.
