from __future__ import annotations

import json
import time
from typing import Optional
import urllib.error
import urllib.parse
import urllib.request


BASE_URL = "http://localhost:3005"


def request_tool(method: str, route: str, payload: Optional[dict] = None, query: Optional[dict] = None) -> dict:
    url = f"{BASE_URL}{route}"
    if query:
        url = f"{url}?{urllib.parse.urlencode(query)}"

    data = None
    headers = {}
    if payload is not None:
        data = json.dumps(payload).encode("utf-8")
        headers["Content-Type"] = "application/json"

    req = urllib.request.Request(url, data=data, headers=headers, method=method.upper())
    try:
        with urllib.request.urlopen(req) as res:
            return json.loads(res.read().decode("utf-8"))
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8")
        if body:
            return json.loads(body)
        raise
    except urllib.error.URLError as exc:
        raise RuntimeError(
            f"Could not reach the Avalonia IntegrationServer at {url}. Is the packaged app running?"
        ) from exc


def emit_response(response: dict, as_json: bool) -> None:
    if as_json:
        print(json.dumps(response, indent=2))
        return

    if response.get("ok"):
        print(f"OK {response.get('tool')}")
        if response.get("data") is not None:
            print(json.dumps(response["data"], indent=2))
    else:
        error = response.get("error") or {}
        print(f"ERROR {error.get('code')}: {error.get('message')}")


def follow_download(download_id: str, as_json: bool) -> dict:
    seen: set[str] = set()
    last_response: dict = {}

    while True:
        response = request_tool(
            "GET",
            "/nexuscentral/get_logs",
            query={"category": "downloads", "download_id": download_id, "limit": 200},
        )
        last_response = response
        lines = response.get("data", {}).get("lines", [])
        for line in lines:
            if line not in seen:
                seen.add(line)
                if not as_json:
                    print(line)

        joined = "\n".join(lines)
        if "Download completed." in joined or "Download failed." in joined:
            return response
        time.sleep(1.0)
