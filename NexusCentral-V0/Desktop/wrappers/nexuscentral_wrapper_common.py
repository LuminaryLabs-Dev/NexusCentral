import json
import os
import sys
import urllib.error
import urllib.request
from pathlib import Path


def emit(event_type, text="", **extra):
    payload = {"type": event_type, "text": text}
    payload.update(extra)
    print(json.dumps(payload), flush=True)


def load_spec(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def call_thinking(spec, prompt):
    url = spec["nexuscentral_base_url"].rstrip("/") + spec.get("llm_tool_route", "/nexuscentral/thinking")
    payload = {
        "prompt": prompt,
        "model_id": spec.get("model_id") or "",
        "max_tokens": int(spec.get("max_tokens") or 1200),
    }
    data = json.dumps(payload).encode("utf-8")
    request = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"}, method="POST")
    with urllib.request.urlopen(request, timeout=300) as response:
        return json.loads(response.read().decode("utf-8"))


def read_artifact_texts(paths, limit=3, bytes_limit=12000):
    summaries = []
    for path in list(paths or [])[:limit]:
        try:
            file_path = Path(path)
            if not file_path.is_file():
                continue
            raw = file_path.read_text(encoding="utf-8", errors="ignore")
            summaries.append(f"# {file_path.name}\n{raw[:bytes_limit]}")
        except Exception as exc:
            summaries.append(f"# {path}\n[read error: {exc}]")
    return "\n\n".join(summaries).strip()


def ensure_output_dir(spec):
    output_dir = Path(spec["output_dir"])
    output_dir.mkdir(parents=True, exist_ok=True)
    return output_dir


def write_artifact(output_dir, name, text):
    path = output_dir / name
    path.write_text(text, encoding="utf-8")
    emit("artifact", label=path.name, path=str(path), kind="file")
    return path


def handle_failure(message, exit_code=1):
    emit("error", message)
    sys.exit(exit_code)


def unwrap_output(response):
    if not response.get("ok"):
        error = (response.get("error") or {}).get("message") or "Unknown tool failure"
        raise RuntimeError(error)
    return response.get("data") or {}
