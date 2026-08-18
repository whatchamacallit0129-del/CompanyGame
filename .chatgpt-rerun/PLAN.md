# Rerun Plan

## Overall Project Goal
Build the Unity 2D company-management simulation as a soft-coded, modular, extensible system. Preserve the existing overall roadmap: company management, corridor/process infrastructure, employees, production systems, day progression, policies, events, and later world/story systems. Do not replace the broader plan with a narrow movement-only project.

## Current Development Focus
The immediate development focus is **Corridor/Node infrastructure + Employee Movement**. Rerun should repeatedly work toward making these two foundations reliable and testable before moving on to later gameplay systems.

### Task A — Corridor / Node System
Build and refine a reusable corridor and navigation-node system.

Requirements:
- Corridors are reusable scene components rather than one-off hard-coded objects.
- Nodes belong to corridors and can be created/edited through reusable tooling.
- Corridor-to-corridor connection should be simple and understandable: select a corridor, enter edit mode, then select another corridor to connect them.
- Connected node visualization should remain clear: unconnected/available nodes use a distinct blue state and connected nodes use a distinct green state.
- Nodes and connections must represent actual navigation data, not only editor visuals.
- The navigation graph must not depend on a specific corridor implementation.
- Support multiple corridors, branches, junctions, and future floors without rewriting movement code.
- Provide useful editor visualization/toggles so corridors/nodes remain visible while editing.
- Avoid requiring the user to manually place a matching node on both corridors just to establish a connection.

Acceptance criteria:
1. Multiple corridors can exist simultaneously and remain visible.
2. Nodes can be added to any corridor through reusable tooling.
3. Two corridors can be connected through the intended edit-mode interaction.
4. Connection state is visibly obvious.
5. Navigation code can query the resulting graph without knowing which corridor created a node.
6. Existing verified corridor/node functionality is preserved unless a concrete bug requires a change.

### Task B — Employee Movement System
Build a reusable employee movement system inspired by the **interaction model and practical feel of Lobotomy Corporation**, without copying proprietary code or assets.

Target interaction:
- Select one employee by clicking them.
- The selected employee has clear visual feedback.
- Click a valid corridor/node destination to issue a movement order.
- The employee finds a path through the connected Node graph.
- The employee follows the path automatically.
- Movement should support corridor branches and future multi-floor navigation.
- Invalid/unreachable destinations should fail gracefully and provide clear feedback rather than silently doing nothing.
- The player should not need to manually manipulate individual nodes to make an employee move.
- The system should be simple enough that the user can understand and operate it without technical knowledge.

The implementation should be modular:
- Employee selection is separate from movement.
- Movement is separate from navigation/pathfinding.
- Navigation is separate from corridor/editor tooling.
- Employee identity/data is separate from movement behavior.
- Input handling is separate from pathfinding logic.
- Runtime movement should be data-driven and configurable rather than employee-specific hardcoding.

Movement configuration should be soft-coded where practical, including:
- movement speed
- stopping distance
- path-following tolerance
- destination validation
- movement priorities/queues where needed later
- animation hooks
- blocked/unreachable behavior
- future floor/transition behavior

Acceptance criteria:
1. A runtime employee can be selected.
2. A destination can be selected through the intended player interaction.
3. A path is generated from the shared Node graph.
4. The employee visibly moves along that path.
5. Multiple employees can use the same navigation system independently.
6. No employee-specific movement code is required.
7. Invalid/unreachable movement requests produce understandable feedback.
8. The implementation remains extensible for later work/production/AI systems.

## Testing / Iteration Loop
Rerun may repeatedly inspect, implement, test, and repair these systems. When an error is encountered:
1. Inspect the actual source and result/error information.
2. Identify the root cause.
3. Make the smallest modular fix that preserves existing behavior.
4. Re-check dependent systems.
5. Record what was verified and what remains unverified.

Do not claim Unity runtime success from GitHub source inspection alone. Runtime behavior must be validated through Unity/available test output.

## Visibility / Usability
All editor tools and runtime feedback should prioritize readability. Visual states, selected objects, nodes, connections, destinations, and movement targets should be obvious at a glance. Prefer simple interactions over technically sophisticated workflows when both are possible.

## Soft-Coding Rules
- Avoid hard-coded employee names, object instance IDs, scene-specific coordinates, or one-off object references.
- Prefer reusable components, interfaces/services, serialized configuration, data assets, registries, and graph queries.
- New systems should depend on abstractions rather than concrete corridor instances.
- Existing command/Unity automation may be used, but do not redesign or modify the Rerun extension itself as part of this plan.
- Preserve compatibility with the existing Command Agent and Auto Pull pipeline.

## Overall Roadmap After Movement Foundation
The broader roadmap remains intact. After Corridor/Node + Employee Movement are reliable, continue toward:
1. Employee work/assignment system.
2. Production/process infrastructure.
3. Resource/material flow.
4. Company management and finances.
5. Day-end/day-start progression and save opportunities.
6. Policies and decision windows.
7. Events, investigations, and reputation.
8. Story/worldbuilding progression and ethical dilemma systems.
9. Expansion to additional departments/floors/process chains.

## Safety Constraints
- Do not modify the Rerun extension/program itself unless the user explicitly asks for that.
- Do not delete or rename verified gameplay systems merely for cleanup.
- Do not change Unity project settings unless required and explicitly justified.
- Before risky gameplay source changes, create or update a clearly named backup snapshot outside the Rerun extension.
- Prefer additive, modular changes.
- Never use a `working` control status.

## Existing Backup
A pre-workflow-change snapshot exists at `.chatgpt-rerun/BACKUP-20260819-seq0.md`. Preserve it. Additional backups should be clearly named and should not overwrite historical snapshots.
