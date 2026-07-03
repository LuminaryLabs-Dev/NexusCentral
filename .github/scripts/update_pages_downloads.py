#!/usr/bin/env python3
import argparse
import html
import json
import re
import shutil
from datetime import datetime, timezone
from pathlib import Path


def slugify(value: str) -> str:
    slug = re.sub(r"[^A-Za-z0-9._-]+", "-", value.strip()).strip("-").lower()
    return slug or "unknown"


def format_size(size_bytes: int) -> str:
    units = ["B", "KB", "MB", "GB"]
    size = float(size_bytes)
    for unit in units:
        if size < 1024 or unit == units[-1]:
            return f"{size:.1f} {unit}" if unit != "B" else f"{int(size)} B"
        size /= 1024
    return f"{size_bytes} B"


def read_size_manifest(path: Path) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {}


def collect_downloads(branch_dir: Path, site_dir: Path) -> list[dict]:
    downloads = []
    for zip_path in sorted(branch_dir.glob("*.zip")):
        stem = zip_path.stem
        size_path = branch_dir / f"{stem}.size.json"
        size_manifest = read_size_manifest(size_path) if size_path.exists() else {}
        rel_zip = zip_path.relative_to(site_dir).as_posix()
        rel_size = size_path.relative_to(site_dir).as_posix() if size_path.exists() else None
        downloads.append(
            {
                "archiveName": zip_path.name,
                "archivePath": rel_zip,
                "sizeManifestPath": rel_size,
                "runtime": size_manifest.get("runtime"),
                "versionDir": size_manifest.get("versionDir"),
                "appVersion": size_manifest.get("appVersion"),
                "archiveBytes": size_manifest.get("archiveBytes", zip_path.stat().st_size),
                "archiveSize": format_size(size_manifest.get("archiveBytes", zip_path.stat().st_size)),
                "archiveSha256": size_manifest.get("archiveSha256"),
            }
        )
    return downloads


def load_branch_records(site_dir: Path) -> list[dict]:
    records = []
    for branch_json in sorted((site_dir / "branches").glob("*.json")):
        try:
            records.append(json.loads(branch_json.read_text(encoding="utf-8")))
        except (OSError, json.JSONDecodeError):
            continue
    return sorted(records, key=lambda item: item.get("branchName", ""))


def render_index(records: list[dict], generated_at: str, repository: str) -> str:
    branch_sections = []
    for record in records:
        branch_name = html.escape(record.get("branchName", "unknown"))
        branch_slug = html.escape(record.get("branchSlug", "unknown"))
        commit = html.escape(record.get("commitSha", "")[:12])
        run_url = html.escape(record.get("runUrl", ""))
        updated = html.escape(record.get("generatedAt", ""))
        rows = []
        for item in record.get("downloads", []):
            archive_name = html.escape(item.get("archiveName", "download.zip"))
            archive_path = html.escape(item.get("archivePath", "#"))
            size_path = item.get("sizeManifestPath")
            size_link = (
                f'<a href="{html.escape(size_path)}">manifest</a>'
                if size_path
                else "manifest"
            )
            rows.append(
                "<tr>"
                f'<td><a href="{archive_path}">{archive_name}</a></td>'
                f"<td>{html.escape(str(item.get('runtime') or 'unknown'))}</td>"
                f"<td>{html.escape(str(item.get('versionDir') or 'unknown'))}</td>"
                f"<td>{html.escape(str(item.get('appVersion') or 'unknown'))}</td>"
                f"<td>{html.escape(str(item.get('archiveSize') or 'unknown'))}</td>"
                f"<td>{size_link}</td>"
                "</tr>"
            )
        if not rows:
            rows.append('<tr><td colspan="6">No downloads published for this branch yet.</td></tr>')
        branch_sections.append(
            f"""
            <section class="branch" id="{branch_slug}">
              <div class="branch-header">
                <div>
                  <h2>{branch_name}</h2>
                  <p>Commit <code>{commit}</code> · updated {updated}</p>
                </div>
                <a class="run-link" href="{run_url}">workflow run</a>
              </div>
              <table>
                <thead>
                  <tr>
                    <th>Download</th>
                    <th>Target</th>
                    <th>Lane</th>
                    <th>Version</th>
                    <th>Size</th>
                    <th>Proof</th>
                  </tr>
                </thead>
                <tbody>{''.join(rows)}</tbody>
              </table>
            </section>
            """
        )

    return f"""<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>NexusCentral Downloads</title>
  <style>
    :root {{
      color-scheme: light dark;
      --bg: #f6f7f9;
      --panel: #ffffff;
      --text: #17191f;
      --muted: #646b78;
      --line: #d8dde6;
      --accent: #1267b1;
    }}
    @media (prefers-color-scheme: dark) {{
      :root {{
        --bg: #101216;
        --panel: #181b21;
        --text: #f1f4f8;
        --muted: #a9b0bd;
        --line: #2c323c;
        --accent: #78b7ff;
      }}
    }}
    * {{ box-sizing: border-box; }}
    body {{
      margin: 0;
      font: 15px/1.5 -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
      background: var(--bg);
      color: var(--text);
    }}
    header, main {{
      max-width: 1180px;
      margin: 0 auto;
      padding: 24px;
    }}
    header {{
      padding-top: 36px;
      padding-bottom: 12px;
    }}
    h1 {{
      margin: 0 0 8px;
      font-size: 32px;
      letter-spacing: 0;
    }}
    h2 {{
      margin: 0;
      font-size: 20px;
      letter-spacing: 0;
    }}
    p {{
      margin: 0;
      color: var(--muted);
    }}
    .branch {{
      background: var(--panel);
      border: 1px solid var(--line);
      border-radius: 8px;
      margin: 0 0 18px;
      overflow: hidden;
    }}
    .branch-header {{
      display: flex;
      justify-content: space-between;
      gap: 16px;
      padding: 18px;
      border-bottom: 1px solid var(--line);
    }}
    .run-link, a {{
      color: var(--accent);
      text-decoration: none;
    }}
    table {{
      width: 100%;
      border-collapse: collapse;
    }}
    th, td {{
      text-align: left;
      padding: 11px 14px;
      border-bottom: 1px solid var(--line);
      vertical-align: top;
    }}
    th {{
      font-size: 12px;
      color: var(--muted);
      text-transform: uppercase;
      letter-spacing: .04em;
    }}
    tr:last-child td {{
      border-bottom: 0;
    }}
    code {{
      font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
      font-size: .94em;
    }}
    @media (max-width: 760px) {{
      header, main {{ padding-left: 14px; padding-right: 14px; }}
      .branch-header {{ display: block; }}
      .run-link {{ display: inline-block; margin-top: 10px; }}
      table {{ display: block; overflow-x: auto; }}
    }}
  </style>
</head>
<body>
  <header>
    <h1>NexusCentral Downloads</h1>
    <p>Public latest build artifacts by branch for {html.escape(repository)}. Generated {html.escape(generated_at)}.</p>
  </header>
  <main>
    {''.join(branch_sections) if branch_sections else '<section class="branch"><div class="branch-header"><p>No downloads published yet.</p></div></section>'}
  </main>
</body>
</html>
"""


def render_readme(records: list[dict], repository: str) -> str:
    lines = [
        "# NexusCentral Downloads",
        "",
        f"Public latest build artifacts by branch for `{repository}`.",
        "",
    ]
    for record in records:
        lines.extend(
            [
                f"## {record.get('branchName', 'unknown')}",
                "",
                f"- Commit: `{record.get('commitSha', '')[:12]}`",
                f"- Workflow: {record.get('runUrl', '')}",
                "",
                "| Download | Target | Lane | Version | Size | Proof |",
                "| --- | --- | --- | --- | ---: | --- |",
            ]
        )
        for item in record.get("downloads", []):
            archive = item.get("archiveName", "download.zip")
            archive_path = item.get("archivePath", "#")
            size_path = item.get("sizeManifestPath") or ""
            proof = f"[{Path(size_path).name}]({size_path})" if size_path else "none"
            lines.append(
                f"| [{archive}]({archive_path}) | {item.get('runtime') or 'unknown'} | "
                f"{item.get('versionDir') or 'unknown'} | {item.get('appVersion') or 'unknown'} | "
                f"{item.get('archiveSize') or 'unknown'} | {proof} |"
            )
        lines.append("")
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--site-dir", required=True)
    parser.add_argument("--artifact-dir", required=True)
    parser.add_argument("--branch-name", required=True)
    parser.add_argument("--commit-sha", required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--run-number", required=True)
    parser.add_argument("--repository", required=True)
    parser.add_argument("--workflow-name", required=True)
    args = parser.parse_args()

    site_dir = Path(args.site_dir).resolve()
    artifact_dir = Path(args.artifact_dir).resolve()
    branch_slug = slugify(args.branch_name)
    generated_at = datetime.now(timezone.utc).replace(microsecond=0).isoformat()
    run_url = f"https://github.com/{args.repository}/actions/runs/{args.run_id}"

    site_dir.mkdir(parents=True, exist_ok=True)
    (site_dir / "downloads").mkdir(exist_ok=True)
    (site_dir / "branches").mkdir(exist_ok=True)

    branch_dir = site_dir / "downloads" / branch_slug / "latest"
    if branch_dir.exists():
        shutil.rmtree(branch_dir.parent)
    branch_dir.mkdir(parents=True, exist_ok=True)

    for source in sorted(artifact_dir.rglob("*")):
        if source.is_file() and (source.suffix == ".zip" or source.name.endswith(".size.json")):
            shutil.copy2(source, branch_dir / source.name)

    downloads = collect_downloads(branch_dir, site_dir)
    branch_record = {
        "schemaVersion": 1,
        "branchName": args.branch_name,
        "branchSlug": branch_slug,
        "commitSha": args.commit_sha,
        "runId": args.run_id,
        "runNumber": args.run_number,
        "runUrl": run_url,
        "workflowName": args.workflow_name,
        "generatedAt": generated_at,
        "downloads": downloads,
    }

    (site_dir / "branches" / f"{branch_slug}.json").write_text(
        json.dumps(branch_record, indent=2) + "\n",
        encoding="utf-8",
    )

    records = load_branch_records(site_dir)
    (site_dir / "downloads" / "index.json").write_text(
        json.dumps({"schemaVersion": 1, "generatedAt": generated_at, "branches": records}, indent=2) + "\n",
        encoding="utf-8",
    )
    (site_dir / "index.html").write_text(
        render_index(records, generated_at, args.repository),
        encoding="utf-8",
    )
    (site_dir / "README.md").write_text(
        render_readme(records, args.repository),
        encoding="utf-8",
    )
    (site_dir / ".nojekyll").write_text("", encoding="utf-8")


if __name__ == "__main__":
    main()
