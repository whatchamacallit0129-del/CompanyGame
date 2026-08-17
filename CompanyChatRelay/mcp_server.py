"""STDIO MCP adapter for the local CompanyChatRelay HTTP API."""

from __future__ import annotations

import json
import os
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

from mcp.server import MCPServer


RELAY_BASE_URL = os.environ.get("COMPANY_CHAT_RELAY_URL", "http://127.0.0.1:8765")
HTTP_TIMEOUT_SECONDS = 310

mcp = MCPServer(
    "Company Chat Relay",
    instructions=(
        "Use these tools only for the local D:\\CompanyProject relay. "
        "Run Codex or queue a Unity command only when the user explicitly requests it."
    ),
)


def call_relay(method: str, path: str, body: dict[str, Any] | None = None) -> str:
    """Call the existing relay and preserve its JSON response body."""
    data = json.dumps(body).encode("utf-8") if body is not None else None
    request = Request(
        f"{RELAY_BASE_URL}{path}",
        data=data,
        method=method,
        headers={"Content-Type": "application/json"} if data is not None else {},
    )

    try:
        with urlopen(request, timeout=HTTP_TIMEOUT_SECONDS) as response:
            return response.read().decode("utf-8")
    except HTTPError as exception:
        return exception.read().decode("utf-8")
    except URLError as exception:
        return json.dumps({"error": f"CompanyChatRelay is unavailable: {exception.reason}"})
    except OSError as exception:
        return json.dumps({"error": f"CompanyChatRelay request failed: {exception}"})


@mcp.tool()
def get_relay_status() -> str:
    """Return the current status JSON from the local CompanyChatRelay."""
    return call_relay("GET", "/status")


@mcp.tool()
def run_codex(prompt: str) -> str:
    """Send an explicit user prompt to the Relay's Codex endpoint and return its JSON result."""
    return call_relay("POST", "/codex", {"prompt": prompt})


@mcp.tool()
def queue_unity_command(command: str) -> str:
    """Queue a supported Unity Command Agent command and return the Relay's JSON result."""
    return call_relay("POST", "/unity", {"command": command})


if __name__ == "__main__":
    mcp.run(transport="stdio")
