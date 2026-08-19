# AI ↔ Unity GitHub Bridge

## Purpose

This is the non-MCP path for letting an external AI workflow operate Unity Editor through the existing GitHub synchronization channel.

```text
ChatGPT
  ↓
GitHub: ai_command.json
  ↓ git pull
D:\CompanyProject
  ↓
CompanyGameAIBridge (Unity Editor)
  ↓
Unity Editor / Hierarchy / Scene / Play Mode
  ↓
results/ai_result.json
  ↓ git commit + push
GitHub
  ↓
ChatGPT reads the result
```

This does **not** use MCP, Cursor, or Codex.

## Files

- `Assets/Editor/CompanyGameAIBridge.cs` — Unity-side executor.
- `ai_unity_bridge_loop.bat` — pulls GitHub commands and pushes Unity results.
- `ai_command.json` — one pending AI command.
- `results/ai_result.json` — latest Unity bridge result.

The existing `Assets/Editor/CompanyGameCommandAgent.cs` and root `command.json` protocol are intentionally left untouched.

## Start

1. Open `D:\CompanyProject` in Unity.
2. Let Unity compile the new `CompanyGameAIBridge.cs`.
3. Stop the old `auto_pull_loop.bat` so two Git processes do not pull at the same time.
4. Run:

```bat
D:\CompanyProject\ai_unity_bridge_loop.bat
```

The loop checks GitHub every 3 seconds.

## Command format

JSON is recommended:

```json
{"command":"CREATE_GAMEOBJECT","args":"Employee"}
```

Supported commands:

```text
PING
CREATE_GAMEOBJECT:name
DELETE_GAMEOBJECT:name
RENAME_GAMEOBJECT:oldName:newName
SET_ACTIVE:name:true|false
SET_POSITION:name:x:y:z
SET_ROTATION:name:x:y:z
SET_SCALE:name:x:y:z
ADD_COMPONENT:name:ComponentType
REMOVE_COMPONENT:name:ComponentType
GET_HIERARCHY
GET_OBJECT_INFO:name
GET_CONSOLE
PLAY
STOP
SAVE_SCENE
```

The bridge executes commands on Unity's editor update thread, so UnityEditor API calls are not made from a background thread.

## Result format

`results/ai_result.json` contains:

- `success`
- `command`
- `message`
- `exception`
- `data`

The loop commits only the bridge queue/result files. It does not stage unrelated project work.

## First test

The repository currently contains a harmless `PING` test command in `ai_command.json`.

After the new bridge loop is started and Unity has compiled the bridge, Unity should consume the command and write:

```json
{
  "success": true,
  "message": "CompanyGame AI Bridge is running."
}
```

The loop then pushes that result back to GitHub.

## Important limitation

This creates a reliable **two-way project channel through GitHub**, not a magical direct socket from this ChatGPT browser session to Windows. ChatGPT can write/read the GitHub queue when the connected GitHub integration has permission, while the local bridge loop performs the actual `git pull`/`git push` and Unity Editor executes the commands.
