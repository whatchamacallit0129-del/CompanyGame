from __future__ import annotations

import json
import os
import shlex
import subprocess
import sys
import time
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
CONFIG_PATH = Path(__file__).with_name("rerun_config.json")


@dataclass
class LoopConfig:
    hard_stop_seconds: int
    checkpoint_seconds: int
    poll_seconds: int
    max_identical_failures: int


class JsonStore:
    def __init__(self, root: Path):
        self.root = root

    def path(self, relative: str) -> Path:
        p = Path(relative)
        return p if p.is_absolute() else self.root / p

    def read(self, relative: str, default: Any = None) -> Any:
        p = self.path(relative)
        if not p.exists():
            return default
        try:
            return json.loads(p.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            return default

    def write(self, relative: str, value: Any) -> None:
        p = self.path(relative)
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")

    def text(self, relative: str, value: str) -> None:
        p = self.path(relative)
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(value, encoding="utf-8")


class EvidenceAnalyzer:
    """Project-independent JSON evidence analyzer.

    It deliberately does not assume a particular Unity error schema. It extracts
    common fields while retaining the complete raw payload for ChatGPT.
    """

    ERROR_WORDS = ("error", "exception", "failed", "failure", "fatal", "invalid", "missing")

    def summarize(self, result: Any, error: Any) -> dict[str, Any]:
        result_text = self._flatten(result)
        error_text = self._flatten(error)
        evidence = error_text or result_text
        category = self._classify(evidence)
        return {
            "category": category,
            "has_result": result is not None,
            "has_error": error is not None,
            "result_summary": result_text[:4000],
            "error_summary": error_text[:8000],
            "raw_result": result,
            "raw_error": error,
        }

    def _flatten(self, value: Any) -> str:
        if value is None:
            return ""
        if isinstance(value, dict):
            parts = []
            for k, v in value.items():
                parts.append(f"{k}: {self._flatten(v)}")
            return " | ".join(parts)
        if isinstance(value, list):
            return " | ".join(self._flatten(v) for v in value)
        return str(value)

    def _classify(self, text: str) -> str:
        t = text.lower()
        if "compile" in t or "cs0" in t:
            return "compile_error"
        if "input" in t or "invalidoperationexception" in t:
            return "input_system_error"
        if "nullreference" in t or "missingreference" in t:
            return "reference_error"
        if "not found" in t or "missing" in t:
            return "missing_dependency"
        if any(w in t for w in self.ERROR_WORDS):
            return "runtime_or_command_error"
        return "unknown"


class RerunEngine:
    def __init__(self, root: Path):
        self.root = root
        self.store = JsonStore(root)
        config = self.store.read(str(CONFIG_PATH.relative_to(root)), {})
        loop = config.get("loop", {})
        self.config = LoopConfig(
            hard_stop_seconds=int(loop.get("hard_stop_minutes", 20) * 60),
            checkpoint_seconds=int(loop.get("checkpoint_minutes", 18) * 60),
            poll_seconds=int(loop.get("poll_seconds", 3)),
            max_identical_failures=int(loop.get("max_identical_failures", 3)),
        )
        self.paths = config.get("paths", {})
        self.bridge = config.get("bridge", {})
        self.analyzer = EvidenceAnalyzer()

    def run(self) -> None:
        run_id = f"local-rerun-{datetime.now().strftime('%Y%m%d-%H%M%S')}-{uuid.uuid4().hex[:8]}"
        state = self.store.read(self.paths["state"], {}) or {}
        task = self.store.read(self.paths["task"], {}) or {}
        state = self._start_state(state, task, run_id)
        self.store.write(self.paths["state"], state)

        started = time.monotonic()
        last_result_stamp = self._stamp(self.paths["result"])
        last_error_stamp = self._stamp(self.paths["error"])
        last_failure_signature = None
        identical_failures = 0
        checkpoint_written = False

        print(f"[RERUN] run_id={run_id}")
        print(f"[RERUN] task={task.get('task_id', 'unspecified')}")

        while True:
            elapsed = time.monotonic() - started
            if elapsed >= self.config.hard_stop_seconds:
                self._terminal(state, "needs_user", "Hard stop reached; inspect current evidence before resuming.")
                return

            if not checkpoint_written and elapsed >= self.config.checkpoint_seconds:
                self._checkpoint(state, elapsed)
                checkpoint_written = True

            result_stamp = self._stamp(self.paths["result"])
            error_stamp = self._stamp(self.paths["error"])
            if result_stamp != last_result_stamp or error_stamp != last_error_stamp:
                last_result_stamp = result_stamp
                last_error_stamp = error_stamp
                result = self.store.read(self.paths["result"])
                error = self.store.read(self.paths["error"])
                evidence = self.analyzer.summarize(result, error)
                self._record_evidence(state, evidence)

                if error is not None:
                    signature = json.dumps({"category": evidence["category"], "error": evidence["error_summary"]}, ensure_ascii=False, sort_keys=True)
                    if signature == last_failure_signature:
                        identical_failures += 1
                    else:
                        last_failure_signature = signature
                        identical_failures = 1
                    if identical_failures >= self.config.max_identical_failures:
                        self._terminal(state, "needs_user", "The same failure repeated too many times. ChatGPT review is required.")
                        return
                    self._publish_prompt(task, state, evidence, mode="recover")
                    self._invoke_bridge()
                else:
                    self._publish_prompt(task, state, evidence, mode="continue")
                    self._invoke_bridge()

            time.sleep(self.config.poll_seconds)

    def _start_state(self, state: dict[str, Any], task: dict[str, Any], run_id: str) -> dict[str, Any]:
        sequence = int(state.get("sequence", 0))
        return {
            "version": 1,
            "run_id": run_id,
            "sequence": sequence,
            "status": "continue",
            "task_id": task.get("task_id", state.get("task_id", "unspecified")),
            "started_at": self._now(),
            "checkpoint": state.get("checkpoint", "session started"),
            "next_exact_action": task.get("next_exact_action", "Read the task and inspect actual project evidence."),
            "verification": state.get("verification", []),
            "evidence": state.get("evidence", []),
        }

    def _publish_prompt(self, task: dict[str, Any], state: dict[str, Any], evidence: dict[str, Any], mode: str) -> None:
        prompt = {
            "instruction": "Continue the current CompanyGame task. Read actual project files and evidence before changing anything.",
            "mode": mode,
            "run_id": state["run_id"],
            "sequence": state["sequence"],
            "task_id": state["task_id"],
            "task": task,
            "state": state,
            "evidence": evidence,
            "rules": [
                "Use soft-coded, reusable architecture.",
                "Do not repeat already verified work.",
                "Inspect result.json and error.json directly when present.",
                "Identify root cause before editing.",
                "Make the smallest modular fix that solves the root cause.",
                "Do not modify the Rerun controller itself.",
                "Do not claim Unity runtime success without evidence.",
            ],
        }
        self.store.text(self.paths["prompt"], json.dumps(prompt, ensure_ascii=False, indent=2))

    def _invoke_bridge(self) -> None:
        if not self.bridge.get("enabled") or not self.bridge.get("command"):
            return
        prompt_path = str(self.store.path(self.paths["prompt"]))
        command = self.bridge["command"]
        args = shlex.split(command) + [prompt_path]
        try:
            subprocess.Popen(args, cwd=self.root)
        except OSError as exc:
            print(f"[RERUN] bridge launch failed: {exc}")

    def _record_evidence(self, state: dict[str, Any], evidence: dict[str, Any]) -> None:
        state.setdefault("evidence", []).append({"at": self._now(), "category": evidence["category"], "error_summary": evidence["error_summary"], "result_summary": evidence["result_summary"]})
        self.store.write(self.paths["state"], state)
        history_path = self.store.path(self.paths["history"])
        history_path.parent.mkdir(parents=True, exist_ok=True)
        with history_path.open("a", encoding="utf-8") as f:
            f.write(json.dumps({"at": self._now(), "run_id": state["run_id"], "sequence": state["sequence"], "task_id": state["task_id"], "evidence": evidence}, ensure_ascii=False) + "\n")

    def _checkpoint(self, state: dict[str, Any], elapsed: float) -> None:
        state["checkpoint"] = f"checkpoint at {int(elapsed // 60)}m"
        state["next_exact_action"] = "Inspect current evidence and continue only if the task remains authorized and bounded."
        self.store.write(self.paths["state"], state)

    def _terminal(self, state: dict[str, Any], status: str, reason: str) -> None:
        state["status"] = status
        state["checkpoint"] = reason
        state["next_exact_action"] = "Review the generated prompt/evidence and authorize the next cycle."
        state["updated_at"] = self._now()
        self.store.write(self.paths["state"], state)
        print(f"[RERUN] terminal status={status}: {reason}")

    def _stamp(self, relative: str) -> tuple[bool, int]:
        p = self.store.path(relative)
        try:
            return p.exists(), p.stat().st_mtime_ns
        except OSError:
            return False, 0

    @staticmethod
    def _now() -> str:
        return datetime.now(timezone.utc).astimezone().isoformat(timespec="seconds")


if __name__ == "__main__":
    try:
        RerunEngine(ROOT).run()
    except KeyboardInterrupt:
        print("[RERUN] stopped by user")
        sys.exit(130)
