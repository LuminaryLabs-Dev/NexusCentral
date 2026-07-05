#!/usr/bin/env python3
"""Export the original idea profile pack into a one-go submission bundle."""

from __future__ import annotations

import argparse
import json
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parent
DEFAULT_INPUT = ROOT / "original-idea-profile-pack.json"
DEFAULT_OUTPUT = ROOT / "profile-submission-bundle.json"
DEFAULT_MARKDOWN = ROOT / "profile-submission-bundle.md"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, default=DEFAULT_INPUT, help="Source profile pack JSON path.")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT, help="Destination bundle JSON path.")
    parser.add_argument(
        "--markdown-output",
        type=Path,
        default=DEFAULT_MARKDOWN,
        help="Destination summary markdown path. Use --no-markdown to skip writing it.",
    )
    parser.add_argument("--no-markdown", action="store_true", help="Skip writing the markdown summary.")
    parser.add_argument(
        "--limit",
        type=int,
        default=None,
        help="Optional maximum number of profiles to export in order.",
    )
    parser.add_argument(
        "--id",
        dest="ids",
        action="append",
        default=[],
        help="Optional profile id to include. Repeat to select a custom batch.",
    )
    return parser.parse_args()


def load_pack(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def select_profiles(profiles: list[dict[str, Any]], ids: list[str], limit: int | None) -> tuple[list[dict[str, Any]], str]:
    by_id = {profile["id"]: profile for profile in profiles}
    if ids:
        selected = [by_id[profile_id] for profile_id in ids if profile_id in by_id]
        selection_rule = f"explicit-id-list:{','.join(ids)}"
    elif limit is not None:
        selected = profiles[:limit]
        selection_rule = f"limit:{limit}"
    else:
        selected = profiles
        selection_rule = "all-profiles"
    return selected, selection_rule


def summarize(profiles: list[dict[str, Any]]) -> dict[str, Any]:
    domains = Counter(profile["domain"] for profile in profiles)
    worlds = Counter(profile["specificWorld"] for profile in profiles)
    angles = Counter(profile["originalAngle"] for profile in profiles)
    surfaces = Counter(surface for profile in profiles for surface in profile["nexusCentralValidation"]["surfaces"])
    return {
        "domainCounts": dict(domains),
        "worldCount": len(worlds),
        "angleCount": len(angles),
        "surfaceCounts": dict(surfaces),
        "firstFive": [profile["name"] for profile in profiles[:5]],
    }


def build_markdown(bundle: dict[str, Any]) -> str:
    lines = [
        "# Profile Submission Bundle",
        "",
        f"- Source pack: `{bundle['sourcePack']}`",
        f"- Selection rule: `{bundle['selectionRule']}`",
        f"- Profiles exported: `{bundle['selectedCount']}`",
        f"- Total available: `{bundle['totalCount']}`",
        f"- Exported at UTC: `{bundle['generatedAtUtc']}`",
        "",
        "## Handoff Notes",
        "",
        "- This file is the compact one-go submission wrapper.",
        "- Use the embedded JSON bundle for the full profile payload.",
        "- Keep additional catalog work in `.agent/use-case-catalog/`.",
        "",
        "## First Profiles",
        "",
    ]
    for profile in bundle["profiles"][:20]:
        lines.append(f"- `{profile['id']}` {profile['name']} | {profile['specificWorld']} | {profile['originalAngle']}")
    if bundle["selectedCount"] > 20:
        lines.append(f"- ... and {bundle['selectedCount'] - 20} more profiles")
    return "\n".join(lines) + "\n"


def main() -> None:
    args = parse_args()
    pack = load_pack(args.input)
    selected, selection_rule = select_profiles(pack["profiles"], args.ids, args.limit)

    bundle = {
        "schemaVersion": 1,
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
        "purpose": "One-go submission bundle for the original idea profile pack, kept inside .agent.",
        "sourcePack": str(args.input),
        "selectionRule": selection_rule,
        "totalCount": len(pack["profiles"]),
        "selectedCount": len(selected),
        "bundleMode": "maximal-batch" if selection_rule == "all-profiles" else "filtered-batch",
        "readyForOneGo": True,
        "summary": summarize(selected),
        "profiles": selected,
    }

    args.output.write_text(json.dumps(bundle, indent=2) + "\n", encoding="utf-8")
    if not args.no_markdown:
        args.markdown_output.write_text(build_markdown(bundle), encoding="utf-8")

    print(f"wrote={args.output}")
    if not args.no_markdown:
        print(f"wrote={args.markdown_output}")
    print(f"selected={len(selected)} total={len(pack['profiles'])} rule={selection_rule}")


if __name__ == "__main__":
    main()
