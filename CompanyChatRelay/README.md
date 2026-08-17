# Company Chat Relay

`CompanyChatRelay` is a local-only HTTP relay for a ChatGPT client, the Codex CLI, and the Unity `CompanyGameCommandAgent`.

It listens only on `127.0.0.1` (never on the LAN or public internet) and uses port `8765` by default. It uses only the Python standard library.

Python 3 must be installed and available as either `py` or `python` on `PATH`.

## Start on Windows

Double-click `Start_CompanyChatRelay.bat`, or run this from PowerShell:

```powershell
cd D:\CompanyProject\CompanyChatRelay
py -3 .\relay_server.py
```

To use another port for one launch:

```powershell
$env:COMPANY_CHAT_RELAY_PORT = "8766"
py -3 .\relay_server.py
```

Stop the server with `Ctrl+C` in its console window.

## Endpoints

### `GET /status`

Returns the relay status, configured project root, whether `command.json` is waiting, and whether a `codex` executable is discoverable on `PATH`.

```powershell
Invoke-RestMethod http://127.0.0.1:8765/status
```

### `POST /codex`

Passes `prompt` to `codex exec` with `D:\CompanyProject` as the working directory and returns Codex standard output, standard error, and the exit code.

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri http://127.0.0.1:8765/codex `
  -ContentType 'application/json' `
  -Body '{"prompt":"List the project files without changing anything."}'
```

The relay waits up to five minutes. If Codex is unavailable or cannot start, it returns a JSON error instead of crashing the server.

### `POST /unity`

The Unity Command Agent expects *raw command text* in a file named `D:\CompanyProject\command.json`. Therefore the relay receives JSON but writes its `command` field as that raw text, allowing the existing Agent to process it unchanged.

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri http://127.0.0.1:8765/unity `
  -ContentType 'application/json' `
  -Body '{"command":"CREATE_EMPTY_OBJECT"}'
```

If `command.json` already exists, the relay returns HTTP 409 and does not overwrite it. When Unity successfully processes a command, the existing Command Agent deletes the file.

Current commands are defined by `Assets\Editor\CompanyGameCommandAgent.cs`; this relay does not modify Unity files.

## MCP adapter (STDIO)

`mcp_server.py` exposes the existing Relay as an MCP server. It communicates only over standard input/output; it does not open another network port and does not contain a Secure MCP Tunnel key.

The adapter exposes exactly these tools:

- `get_relay_status` — returns `GET /status` from the Relay.
- `run_codex(prompt)` — returns `POST /codex` from the Relay.
- `queue_unity_command(command)` — returns `POST /unity` from the Relay.

### Install

Install Python 3, then create a virtual environment and install the single MCP dependency:

```powershell
cd D:\CompanyProject\CompanyChatRelay
py -3 -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r .\requirements.txt
```

### Run locally

Start `relay_server.py` first in one terminal. Then double-click `Start_CompanyMcpServer.bat`, or run:

```powershell
cd D:\CompanyProject\CompanyChatRelay
.\.venv\Scripts\python.exe .\mcp_server.py
```

The MCP adapter reserves stdout for the MCP protocol. Do not write ordinary logs to stdout while it is running.

### Secure MCP Tunnel

No tunnel is configured by this project. A future Secure MCP Tunnel client can launch this STDIO server and forward MCP requests without exposing the Relay to the public internet. Keep the tunnel identity and runtime API key outside this folder, such as in environment variables or the tunnel client's secure configuration.
