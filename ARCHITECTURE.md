# CompanyGame Architecture Audit

## Scope
Audit performed for the Rerun `architecture-audit` task. No gameplay source code was modified during this audit.

## Current Structure

### Corridor / Navigation
- `Assets/Scripts/CompanyGameCorridor.cs` owns corridor identity/configuration, its node list, walkability/floor data, and explicit corridor-to-corridor links.
- `Assets/Scripts/CompanyGamePathNode.cs` represents runtime navigation points and explicit node-to-node connections.
- `Assets/Scripts/Pathfinding/CompanyGameNavigationGraph.cs` maintains a runtime-wide node registry and nearest-node queries.
- `Assets/Scripts/Pathfinding/CompanyGameNavigationService.cs` performs graph path searches using node connections and movement cost.

### Employee Movement
- `Assets/Scripts/Movement/CompanyGameEmployeeMovement.cs` owns an employee's runtime path-following and movement configuration.
- `Assets/Scripts/Movement/CompanyGameEmployeeMovementBootstrap.cs` attaches movement behavior to employee objects at runtime.
- `Assets/Scripts/Movement/CompanyGameEmployeeSelectionController.cs` owns player input, single/group selection, selection feedback, and destination commands.

### Existing General Interaction
- `Assets/Scripts/InteractableObject2D.cs`, `DraggableObject2D.cs`, and `SelectionManager2D.cs` provide existing interaction/selection infrastructure that may overlap with employee selection and should be kept compatible rather than duplicated unnecessarily.

## Data Flow

1. Corridor components own authoring relationships to nodes/corridors.
2. Path nodes expose explicit graph connections.
3. Navigation graph discovers runtime nodes.
4. Navigation service converts start/goal positions into nearest nodes and searches the shared graph.
5. Employee movement requests a path and follows its nodes.
6. Employee selection converts player clicks into employee selection and movement requests.

## Strengths
- Corridor data and pathfinding are already separated at a useful boundary.
- Navigation service depends on nodes rather than concrete corridor instances.
- Employee selection and movement are separate components.
- Movement parameters are component configuration rather than employee-specific code.
- The structure can support branches and future floor-aware navigation without rewriting the employee controller.

## Risks / Coupling
1. Corridor links and node links are currently two related representations. The long-term design should define which one is authoritative for navigation and how corridor editing produces node graph connections.
2. `CompanyGameNavigationGraph` discovers nodes globally at runtime. This is simple and extensible for prototypes, but large scenes may eventually benefit from explicit registries or scoped graphs.
3. `CompanyGameNavigationService` uses a list-based shortest-path search rather than a priority queue/A* implementation. This is acceptable for the current prototype but should be replaceable behind the service boundary.
4. Selection currently depends on `Physics2D` and an `employeeLayer` mask. This is appropriate for Unity input, but input and selection policy should remain separate from pathfinding.
5. Runtime movement currently needs Unity/Play Mode verification; source inspection alone cannot prove that scene colliders, layers, node connections, and components are configured correctly.
6. Existing general-purpose selection/drag systems could overlap with the newer employee-specific selection controller. Future cleanup should consolidate behavior only after runtime behavior is verified.

## Recommended Next Tasks

### Task 1 — Corridor/Node authoring verification
Verify and improve the reusable corridor/node editor workflow without changing the Rerun extension. Prioritize visibility and the simple corridor-select/edit/connect interaction.

### Task 2 — Navigation graph integration
Ensure corridor/node authoring creates the runtime graph data required by navigation, with clear rules for connection ownership and bidirectionality.

### Task 3 — Employee movement runtime test
In Unity Play Mode verify employee selection, destination input, path generation, path following, multiple employees, and unreachable destinations. Fix only concrete failures.

### Task 4 — Movement usability pass
Improve selection highlight, destination marker, path feedback, movement state, and failure messages so the system is understandable without technical knowledge.

## Safety Boundary
The Rerun extension/program is outside this project implementation scope and must not be modified. Changes should be additive and modular, with backups before risky gameplay source changes.
