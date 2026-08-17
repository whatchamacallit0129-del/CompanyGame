"""Local-only relay between HTTP clients, Codex CLI, and Unity Command Agent."""

from __future__ import annotations

import json
import os
import shutil
import subprocess
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any


HOST = "127.0.0.1"
PORT = int(os.environ.get("COMPANY_CHAT_RELAY_PORT", "8765"))
PROJECT_ROOT = Path(__file__).resolve().parent.parent
COMMAND_FILE = PROJECT_ROOT / "command.json"
MAX_REQUEST_BYTES = 1_048_576
CODEX_TIMEOUT_SECONDS = 300


class RelayRequestHandler(BaseHTTPRequestHandler):
    server_version = "CompanyChatRelay/1.0"

    def do_GET(self) -> None:
        if self.path == "/status":
            self._send_json(
                HTTPStatus.OK,
                {
                    "status": "ok",
                    "host": HOST,
                    "port": PORT,
                    "project_root": str(PROJECT_ROOT),
                    "pending_unity_command": COMMAND_FILE.exists(),
                    "codex_cli_found": shutil.which("codex") is not None,
                },
            )
            return

        self._send_json(HTTPStatus.NOT_FOUND, {"error": "Not found"})

    def do_POST(self) -> None:
        try:
            payload = self._read_json_body()
        except ValueError as exception:
            self._send_json(HTTPStatus.BAD_REQUEST, {"error": str(exception)})
            return

        if self.path == "/codex":
            self._handle_codex(payload)
            return

        if self.path == "/unity":
            self._handle_unity(payload)
            return

        self._send_json(HTTPStatus.NOT_FOUND, {"error": "Not found"})

    def _handle_codex(self, payload: Any) -> None:
        if not isinstance(payload, dict) or not isinstance(payload.get("prompt"), str):
            self._send_json(
                HTTPStatus.BAD_REQUEST,
                {"error": "Request JSON must contain a string field named 'prompt'."},
            )
            return

        prompt = payload["prompt"].strip()
        if not prompt:
            self._send_json(HTTPStatus.BAD_REQUEST, {"error": "'prompt' must not be empty."})
            return

        codex_path = shutil.which("codex")
        if codex_path is None:
            self._send_json(HTTPStatus.SERVICE_UNAVAILABLE, {"error": "Codex CLI was not found on PATH."})
            return

        try:
            result = subprocess.run(
                [codex_path, "exec", "--skip-git-repo-check", prompt],
                cwd=PROJECT_ROOT,
                text=True,
                capture_output=True,
                timeout=CODEX_TIMEOUT_SECONDS,
                check=False,
            )
        except OSError as exception:
            self._send_json(
                HTTPStatus.SERVICE_UNAVAILABLE,
                {"error": f"Codex CLI could not start: {exception}"},
            )
            return
        except subprocess.TimeoutExpired as exception:
            self._send_json(
                HTTPStatus.GATEWAY_TIMEOUT,
                {
                    "error": f"Codex CLI exceeded the {CODEX_TIMEOUT_SECONDS}-second timeout.",
                    "stdout": exception.stdout or "",
                    "stderr": exception.stderr or "",
                },
            )
            return

        self._send_json(
            HTTPStatus.OK if result.returncode == 0 else HTTPStatus.BAD_GATEWAY,
            {
                "returncode": result.returncode,
                "stdout": result.stdout,
                "stderr": result.stderr,
            },
        )

    def _handle_unity(self, payload: Any) -> None:
        if not isinstance(payload, dict) or not isinstance(payload.get("command"), str):
            self._send_json(
                HTTPStatus.BAD_REQUEST,
                {"error": "Request JSON must contain a string field named 'command'."},
            )
            return

        command = payload["command"].strip()
        if not command:
            self._send_json(HTTPStatus.BAD_REQUEST, {"error": "'command' must not be empty."})
            return

        if COMMAND_FILE.exists():
            self._send_json(
                HTTPStatus.CONFLICT,
                {"error": "A Unity command is already waiting in command.json."},
            )
            return

        try:
            # The Unity agent expects the raw command text, despite its .json filename.
            with COMMAND_FILE.open("x", encoding="utf-8", newline="\n") as command_file:
                command_file.write(command)
        except FileExistsError:
            self._send_json(
                HTTPStatus.CONFLICT,
                {"error": "A Unity command is already waiting in command.json."},
            )
            return
        except OSError as exception:
            self._send_json(
                HTTPStatus.INTERNAL_SERVER_ERROR,
                {"error": f"Unable to create command.json: {exception}"},
            )
            return

        self._send_json(
            HTTPStatus.ACCEPTED,
            {"status": "queued", "command": command, "command_file": str(COMMAND_FILE)},
        )

    def _read_json_body(self) -> Any:
        content_length = self.headers.get("Content-Length")
        if content_length is None:
            raise ValueError("Content-Length header is required.")

        try:
            length = int(content_length)
        except ValueError as exception:
            raise ValueError("Content-Length must be an integer.") from exception

        if length < 0 or length > MAX_REQUEST_BYTES:
            raise ValueError(f"Request body must be between 0 and {MAX_REQUEST_BYTES} bytes.")

        try:
            return json.loads(self.rfile.read(length).decode("utf-8"))
        except UnicodeDecodeError as exception:
            raise ValueError("Request body must be UTF-8.") from exception
        except json.JSONDecodeError as exception:
            raise ValueError("Request body must contain valid JSON.") from exception

    def _send_json(self, status: HTTPStatus, body: dict[str, Any]) -> None:
        encoded_body = json.dumps(body, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(encoded_body)))
        self.end_headers()
        self.wfile.write(encoded_body)

    def log_message(self, format: str, *args: Any) -> None:
        print(f"[{self.log_date_time_string()}] {self.address_string()} {format % args}")


def main() -> None:
    print(f"CompanyChatRelay listening on http://{HOST}:{PORT}")
    print(f"Unity command file: {COMMAND_FILE}")
    ThreadingHTTPServer((HOST, PORT), RelayRequestHandler).serve_forever()


if __name__ == "__main__":
    main()
