# CompanyGame Rerun Controller

A project-local, soft-coded development loop for CompanyGame.

## Purpose

This tool is intentionally independent from the existing ChatGPT Rerun extension. It coordinates a development cycle around the Unity project and Git state, while leaving the actual reasoning and code decisions to ChatGPT.

The controller:

1. Loads the project goal and current task state.
2. Creates a bounded work session.
3. Publishes a structured prompt for ChatGPT.
4. Watches `result.json` and `error.json` (paths are configurable).
5. Reads and summarizes those JSON results itself.
6. Classifies failures into actionable categories.
7. Generates a next-action prompt containing the actual error/result evidence.
8. Tracks retries, checkpoints, terminal states, and verification records.

## Safety

- Never uses a `working` status.
- Hard stop defaults to 20 minutes.
- Checkpoint defaults to 18 minutes.
- Repeated identical failures are capped.
- Existing project files are not modified by the controller unless an explicitly configured bridge is used.
- The controller does not modify the existing `.chatgpt-rerun` protocol or extension.
- GitHub is treated as version/backup storage, not as the only runtime state source.

## Extensibility

Behavior is driven by `rerun_config.json`, task data, and adapters. Do not hard-code employee names, scene coordinates, Unity object names, or individual gameplay implementations into the controller.

The JSON analyzer accepts arbitrary result/error payloads and preserves the raw evidence. New error classifiers can be added without changing the state machine.

## ChatGPT bridge

The controller can invoke an external bridge command if `bridge.command` is configured. The command receives the generated prompt path as its first argument. If no bridge is configured, the controller still produces the prompt and waits for the next cycle.

This separation keeps the Rerun engine independent from any particular ChatGPT client, browser extension, API, or MCP implementation.

## Run

From the repository root:

```text
python Tools/CompanyGameRerun/app.py
```

Or use `start_rerun.bat`.
