#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[1]
SUITE_ROOT = REPO_ROOT / "lab-build-suite"
SUITE_FILE = SUITE_ROOT / "suite.json"
SUITE_B_FILE = SUITE_ROOT / "suite-b.json"
PROMPTS_ROOT = SUITE_ROOT / "prompts"
PROJECTS_ROOT = SUITE_ROOT / "projects"
VALIDATION_ROOT = SUITE_ROOT / "validation"
DEFAULT_HARNESS_SRC = Path.home() / "Documents" / "GitHub" / "NexusGLMHarness" / "src"


@dataclass
class GateResult:
    name: str
    status: str
    message: str = ""
    elapsed_seconds: float = 0.0
    output: str = ""


def load_suite_file(path: Path, pack: str) -> dict[str, Any]:
    suite = json.loads(path.read_text(encoding="utf-8"))
    suite["pack"] = suite.get("pack") or pack
    output_root = REPO_ROOT / suite.get("output_root", "lab-build-suite/projects")
    for project in suite.get("projects", []):
        project["_pack"] = suite["pack"]
        project["_output_root"] = str(output_root)
    return suite


def load_suites(pack: str) -> list[dict[str, Any]]:
    if pack == "a":
        return [load_suite_file(SUITE_FILE, "a")]
    if pack == "b":
        return [load_suite_file(SUITE_B_FILE, "b")]
    if pack == "all":
        return [load_suite_file(SUITE_FILE, "a"), load_suite_file(SUITE_B_FILE, "b")]
    raise SystemExit(f"unknown pack: {pack}")


def project_prompt(project: dict[str, Any]) -> str:
    return (SUITE_ROOT / project["prompt"]).read_text(encoding="utf-8").strip()


def project_dir(project: dict[str, Any]) -> Path:
    output_root = Path(project.get("_output_root") or PROJECTS_ROOT)
    return output_root / project["slug"]


def run_command(
    command: list[str],
    cwd: Path,
    timeout: int,
    env: dict[str, str] | None = None,
) -> GateResult:
    started = time.monotonic()
    try:
        completed = subprocess.run(
            command,
            cwd=str(cwd),
            env=env,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            timeout=timeout,
            check=False,
        )
        elapsed = time.monotonic() - started
        return GateResult(
            name=" ".join(command),
            status="pass" if completed.returncode == 0 else "fail",
            message=f"exit_code={completed.returncode}",
            elapsed_seconds=elapsed,
            output=completed.stdout,
        )
    except subprocess.TimeoutExpired as exc:
        elapsed = time.monotonic() - started
        output = (exc.stdout or "") if isinstance(exc.stdout, str) else ""
        return GateResult(
            name=" ".join(command),
            status="fail",
            message=f"timeout_after={timeout}s",
            elapsed_seconds=elapsed,
            output=output,
        )


def check_suite_specs(suite: dict[str, Any]) -> list[str]:
    issues: list[str] = []
    output_root = REPO_ROOT / suite.get("output_root", "lab-build-suite/projects")
    if not output_root.exists():
        issues.append(f"missing projects directory: {output_root.relative_to(REPO_ROOT)}")
    if not VALIDATION_ROOT.exists():
        issues.append("missing validation directory")

    slugs: set[str] = set()
    for project in suite.get("projects", []):
        slug = str(project.get("slug", "")).strip()
        if not slug:
            issues.append("project missing slug")
            continue
        if slug in slugs:
            issues.append(f"duplicate project slug: {slug}")
        slugs.add(slug)

        prompt_path = SUITE_ROOT / str(project.get("prompt", ""))
        expected_path = project_dir(project) / "EXPECTED_OUTCOME.md"
        if not prompt_path.exists():
            issues.append(f"{slug}: missing prompt {prompt_path.relative_to(REPO_ROOT)}")
        elif not prompt_path.read_text(encoding="utf-8").strip():
            issues.append(f"{slug}: empty prompt")
        if not expected_path.exists():
            issues.append(f"{slug}: missing EXPECTED_OUTCOME.md")
        if project.get("long_file_min_lines", 0) and "long" not in slug:
            issues.append(f"{slug}: long file requirement exists but slug is not marked long")

    if len(slugs) != 10:
        issues.append(f"expected 10 projects, found {len(slugs)}")
    return issues


def source_files(root: Path) -> list[Path]:
    if not root.exists():
        return []
    ignored_parts = {"node_modules", "dist", ".git", ".next"}
    ignored_names = {"EXPECTED_OUTCOME.md", "package-lock.json"}
    files: list[Path] = []
    for path in root.rglob("*"):
        if not path.is_file():
            continue
        if any(part in ignored_parts for part in path.parts):
            continue
        if path.name in ignored_names:
            continue
        if path.suffix.lower() in {".js", ".jsx", ".ts", ".tsx", ".css", ".html", ".json", ".md"}:
            files.append(path)
    return files


def read_project_text(root: Path) -> str:
    chunks: list[str] = []
    for path in source_files(root):
        try:
            chunks.append(path.read_text(encoding="utf-8", errors="ignore"))
        except OSError:
            continue
    return "\n".join(chunks)


def count_lines(path: Path) -> int:
    try:
        return len(path.read_text(encoding="utf-8", errors="ignore").splitlines())
    except OSError:
        return 0


def validate_files(project: dict[str, Any]) -> GateResult:
    root = project_dir(project)
    required = [
        root / "package.json",
        root / "index.html",
        root / "src",
        root / "README.md",
        root / "validation.md",
        root / "harness-report.json",
    ]
    missing = [path.relative_to(REPO_ROOT).as_posix() for path in required if not path.exists()]
    if missing:
        return GateResult("files", "fail", "missing: " + ", ".join(missing))

    empty = [
        path.relative_to(REPO_ROOT).as_posix()
        for path in required
        if path.is_file() and not path.read_text(encoding="utf-8", errors="ignore").strip()
    ]
    if empty:
        return GateResult("files", "fail", "empty: " + ", ".join(empty))

    return GateResult("files", "pass", "required files present")


def validate_long_file(project: dict[str, Any]) -> GateResult:
    minimum = int(project.get("long_file_min_lines") or 0)
    if minimum <= 0:
        return GateResult("long_file", "not_applicable", "no long file required")

    root = project_dir(project)
    candidates = [path for path in source_files(root) if path.suffix.lower() in {".js", ".jsx", ".ts", ".tsx"}]
    longest = max(candidates, key=count_lines, default=None)
    line_count = count_lines(longest) if longest else 0
    if longest and line_count >= minimum:
        return GateResult(
            "long_file",
            "pass",
            f"{longest.relative_to(root).as_posix()} has {line_count} lines",
        )
    found = f"{longest.relative_to(root).as_posix()} has {line_count} lines" if longest else "no source files found"
    return GateResult("long_file", "fail", f"expected >= {minimum}; {found}")


def validate_static_human_view(project: dict[str, Any]) -> GateResult:
    root = project_dir(project)
    if not (root / "src").exists():
        return GateResult("human_view_static", "fail", "missing generated src directory")
    text = read_project_text(root)
    lowered = text.lower()
    missing_terms = [term for term in project.get("required_terms", []) if str(term).lower() not in lowered]
    if missing_terms:
        return GateResult("human_view_static", "fail", "missing terms: " + ", ".join(missing_terms))

    forbidden = ["https://", "http://", "cdn.jsdelivr", "unpkg.com", "cdnjs.cloudflare"]
    found_forbidden = [term for term in forbidden if term in lowered]
    if found_forbidden:
        return GateResult("human_view_static", "fail", "remote asset reference: " + ", ".join(found_forbidden))

    return GateResult("human_view_static", "pass", "required interaction terms and local-only asset policy present")


def validate_three_dependency(project: dict[str, Any]) -> GateResult:
    if project.get("kind") != "threejs-game":
        return GateResult("three_dependency", "not_applicable", "not a Three.js project")
    package_path = project_dir(project) / "package.json"
    if not package_path.exists():
        return GateResult("three_dependency", "fail", "missing package.json")
    try:
        package = json.loads(package_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        return GateResult("three_dependency", "fail", f"invalid package.json: {exc}")
    dependencies = package.get("dependencies") or {}
    if "three" in dependencies:
        return GateResult("three_dependency", "pass", "three dependency present")
    return GateResult("three_dependency", "fail", "three dependency missing")


def npm_install_if_needed(root: Path, timeout: int) -> GateResult:
    if (root / "node_modules").exists():
        return GateResult("npm_install", "pass", "node_modules already present")
    return run_command(["npm", "install", "--no-audit", "--no-fund"], root, timeout)


def build_project(root: Path, timeout: int) -> GateResult:
    if not (root / "package.json").exists():
        return GateResult("compiler", "fail", "missing package.json")
    install = npm_install_if_needed(root, timeout)
    if install.status != "pass":
        install.name = "npm_install"
        return install
    result = run_command(["npm", "run", "build"], root, timeout)
    result.name = "compiler"
    return result


def package_scripts(root: Path) -> dict[str, str]:
    package_path = root / "package.json"
    if not package_path.exists():
        return {}
    try:
        package = json.loads(package_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return {}
    scripts = package.get("scripts")
    return {str(key): str(value) for key, value in scripts.items()} if isinstance(scripts, dict) else {}


def run_package_script(root: Path, script_name: str, timeout: int) -> GateResult:
    scripts = package_scripts(root)
    if script_name not in scripts:
        return GateResult(script_name, "not_applicable", f"package.json has no {script_name} script")
    if not (root / "node_modules").exists():
        install = npm_install_if_needed(root, timeout)
        if install.status != "pass":
            install.name = f"{script_name}_install"
            return install
    result = run_command(["npm", "run", script_name], root, timeout)
    result.name = script_name
    return result


def harness_env(harness_src: Path) -> dict[str, str]:
    env = os.environ.copy()
    current = env.get("PYTHONPATH", "")
    entries = [str(harness_src)]
    if current:
        entries.append(current)
    env["PYTHONPATH"] = os.pathsep.join(entries)
    return env


def run_harness(project: dict[str, Any], args: argparse.Namespace) -> GateResult:
    root = project_dir(project)
    root.mkdir(parents=True, exist_ok=True)
    prompt = project_prompt(project)
    goal = (
        f"{prompt}\n\n"
        "Write the complete project into the current workspace root. "
        "Do not summarize instead of writing files. "
        "After writing files, create README.md, validation.md, and harness-report.json."
    )
    command = [
        sys.executable,
        "-m",
        "nexus_glm_harness",
        "run",
        "--workspace",
        str(root),
        "--goal",
        goal,
        "--max-iterations",
        str(args.max_iterations),
        "--max-runtime-seconds",
        str(args.max_runtime_seconds),
        "--max-model-calls",
        str(args.max_model_calls),
        "--context-budget-chars",
        str(args.context_budget_chars),
    ]
    if args.no_build:
        command.append("--no-build")
    else:
        command.append("--build")
    if args.offline:
        command.append("--offline")
    result = run_command(command, root, args.max_runtime_seconds + 60, env=harness_env(args.harness_src))
    result.name = "harness_run"
    return result


def repair_project(project: dict[str, Any], args: argparse.Namespace) -> GateResult:
    root = project_dir(project)
    command = [
        sys.executable,
        "-m",
        "nexus_glm_harness",
        "repair",
        "--workspace",
        str(root),
        "--goal",
        project_prompt(project),
        "--max-iterations",
        str(args.max_iterations),
    ]
    if args.no_build:
        command.append("--no-build")
    else:
        command.append("--build")
    result = run_command(command, root, args.max_runtime_seconds + 60, env=harness_env(args.harness_src))
    result.name = "repair"
    return result


def cli_validate(project: dict[str, Any], args: argparse.Namespace) -> GateResult:
    root = project_dir(project)
    command = [
        sys.executable,
        "-m",
        "nexus_glm_harness",
        "validate",
        "--workspace",
        str(root),
        "--goal",
        project_prompt(project),
    ]
    if args.no_build:
        command.append("--no-build")
    else:
        command.append("--build")
    result = run_command(command, root, args.max_runtime_seconds, env=harness_env(args.harness_src))
    result.name = "cli_validation"
    return result


def validate_project(project: dict[str, Any], args: argparse.Namespace) -> dict[str, Any]:
    root = project_dir(project)
    gates: list[GateResult] = [
        validate_files(project),
        validate_long_file(project),
        validate_static_human_view(project),
        validate_three_dependency(project),
    ]
    if args.compiler and (root / "package.json").exists():
        gates.append(build_project(root, args.build_timeout))
    else:
        gates.append(GateResult("compiler", "not_run", "use --compiler to run npm install/build"))
    if args.lint and (root / "package.json").exists():
        gates.append(run_package_script(root, "lint", args.build_timeout))
    else:
        gates.append(GateResult("lint", "not_run", "use --lint to run npm run lint when present"))
    if args.test and (root / "package.json").exists():
        gates.append(run_package_script(root, "test", args.build_timeout))
    else:
        gates.append(GateResult("test", "not_run", "use --test to run npm test when present"))
    if args.cli and root.exists():
        gates.append(cli_validate(project, args))
    else:
        gates.append(GateResult("cli_validation", "not_run", "use --cli to run nexus_glm_harness validate"))

    failed = [gate for gate in gates if gate.status == "fail"]
    return {
        "pack": project.get("_pack", "a"),
        "slug": project["slug"],
        "status": "pass" if not failed else "fail",
        "kind": project["kind"],
        "stack": project["stack"],
        "long_file_min_lines": project.get("long_file_min_lines", 0),
        "gates": [gate.__dict__ for gate in gates],
        "failure_class": classify_failure(gates),
        "final_tree": ascii_tree(root, max_depth=3) if root.exists() else "",
    }


def run_project(project: dict[str, Any], args: argparse.Namespace) -> dict[str, Any]:
    run_result = run_harness(project, args)
    repair_results: list[GateResult] = []
    validation = validate_project(project, args)
    attempts = 0
    while validation["status"] != "pass" and attempts < args.repair_attempts:
        attempts += 1
        repair_results.append(repair_project(project, args))
        validation = validate_project(project, args)
    validation["harness_run"] = run_result.__dict__
    validation["repair_count"] = attempts
    validation["repairs"] = [result.__dict__ for result in repair_results]
    return validation


def classify_failure(gates: list[GateResult]) -> str:
    failed = {gate.name: gate for gate in gates if gate.status == "fail"}
    if "files" in failed:
        return "missing_files"
    if "long_file" in failed:
        return "long_file_gap"
    if "compiler" in failed or "npm_install" in failed or "lint" in failed or "test" in failed:
        return "compile_error"
    if "cli_validation" in failed:
        return "repair_loop_gap"
    if "human_view_static" in failed:
        message = failed["human_view_static"].message
        return "state_gap" if "localStorage" in message else "behavior_gap"
    if "three_dependency" in failed:
        return "dependency_gap"
    return "none"


def ascii_tree(root: Path, max_depth: int = 3) -> str:
    if not root.exists():
        return f"{root.name}/ MISSING"
    lines = [f"{root.name}/"]

    def walk(directory: Path, prefix: str, depth: int) -> None:
        if depth >= max_depth:
            return
        entries = sorted(
            [path for path in directory.iterdir() if path.name not in {"node_modules", "dist", ".git", ".next"}],
            key=lambda path: (not path.is_dir(), path.name.lower()),
        )
        for index, path in enumerate(entries):
            connector = "`-- " if index == len(entries) - 1 else "|-- "
            lines.append(f"{prefix}{connector}{path.name}{'/' if path.is_dir() else ''}")
            if path.is_dir():
                extension = "    " if index == len(entries) - 1 else "|   "
                walk(path, prefix + extension, depth + 1)

    walk(root, "", 0)
    return "\n".join(lines)


def write_reports(results: list[dict[str, Any]], suites: list[dict[str, Any]]) -> None:
    VALIDATION_ROOT.mkdir(parents=True, exist_ok=True)
    overall = "pass" if results and all(result["status"] == "pass" for result in results) else "fail"
    matrix = {
        "suite_id": "+".join(suite["suite_id"] for suite in suites),
        "status": overall,
        "project_count": len(results),
        "projects": results,
    }
    (VALIDATION_ROOT / "acceptance-matrix.json").write_text(json.dumps(matrix, indent=2), encoding="utf-8")

    compiler_lines = ["# Compiler Results", ""]
    cli_lines = ["# CLI Results", ""]
    human_lines = ["# Human View Results", ""]
    repair_lines = ["# Repair Log", ""]
    final_lines = [
        "# GLM Harness Reliability Suite",
        "",
        f"Status: {overall.upper()}",
        "",
        "## Suite Artifact Structure",
        "",
        "```text",
        "lab-build-suite/",
        "|-- prompts/",
        "|-- prompts-b/",
        "|-- projects/",
        "|-- projects-b/",
        "|-- validation/",
        "`-- final-report.md",
        "```",
        "",
        "## Completed Generated Projects",
        "",
        "```text",
        "projects/",
    ]

    for index, result in enumerate(results):
        slug = result["slug"]
        status = result["status"].upper()
        connector = "`--" if index == len(results) - 1 else "|--"
        final_lines.append(f"{connector} [{result.get('pack', 'a')}] {slug}/ {status} {result.get('failure_class', '')}")
        for gate in result.get("gates", []):
            line = f"- {slug}: {gate['status']} - {gate['message']}"
            if gate["name"] in {"compiler", "lint", "test"}:
                compiler_lines.append(line)
            elif gate["name"] == "cli_validation":
                cli_lines.append(line)
            elif gate["name"] in {"human_view_static", "long_file", "three_dependency", "files"}:
                human_lines.append(f"- {slug} [{gate['name']}]: {gate['status']} - {gate['message']}")
        repair_lines.append(f"- {slug}: repair_count={result.get('repair_count', 0)} failure_class={result.get('failure_class', 'none')}")

    final_lines.extend(["```", "", "## Project Trees", ""])
    for result in results:
        final_lines.extend([f"### {result['slug']}", "", "```text", result.get("final_tree", ""), "```", ""])

    (VALIDATION_ROOT / "compiler-results.md").write_text("\n".join(compiler_lines) + "\n", encoding="utf-8")
    (VALIDATION_ROOT / "cli-results.md").write_text("\n".join(cli_lines) + "\n", encoding="utf-8")
    (VALIDATION_ROOT / "human-view-results.md").write_text("\n".join(human_lines) + "\n", encoding="utf-8")
    (VALIDATION_ROOT / "repair-log.md").write_text("\n".join(repair_lines) + "\n", encoding="utf-8")
    (SUITE_ROOT / "final-report.md").write_text("\n".join(final_lines) + "\n", encoding="utf-8")


def select_projects(suite: dict[str, Any], slugs: list[str]) -> list[dict[str, Any]]:
    projects = suite.get("projects", [])
    if not slugs:
        return projects
    wanted = set(slugs)
    selected = [project for project in projects if project["slug"] in wanted]
    missing = wanted - {project["slug"] for project in selected}
    if missing:
        raise SystemExit("unknown project slug(s): " + ", ".join(sorted(missing)))
    return selected


def select_projects_from_suites(suites: list[dict[str, Any]], slugs: list[str]) -> list[dict[str, Any]]:
    projects = [project for suite in suites for project in suite.get("projects", [])]
    if not slugs:
        return projects
    wanted = set(slugs)
    selected = [project for project in projects if project["slug"] in wanted]
    missing = wanted - {project["slug"] for project in selected}
    if missing:
        raise SystemExit("unknown project slug(s): " + ", ".join(sorted(missing)))
    return selected


def main(argv: list[str] | None = None) -> int:
    argv = normalize_argv(list(argv if argv is not None else sys.argv[1:]))
    parser = argparse.ArgumentParser(description="Run the GLM harness reliability suite.")
    parser.add_argument("--harness-src", type=Path, default=DEFAULT_HARNESS_SRC)
    parser.add_argument("--max-iterations", type=int, default=3)
    parser.add_argument("--repair-attempts", type=int, default=3)
    parser.add_argument("--max-runtime-seconds", type=int, default=1800)
    parser.add_argument("--max-model-calls", type=int, default=20)
    parser.add_argument("--context-budget-chars", type=int, default=60000)
    parser.add_argument("--build-timeout", type=int, default=180)
    parser.add_argument("--no-build", action="store_true")
    parser.add_argument("--offline", action="store_true")
    parser.add_argument("--pack", choices=["a", "b", "all"], default="a")
    sub = parser.add_subparsers(dest="command", required=True)

    sub.add_parser("check-specs", help="Validate prompt and expected-output suite definitions.")

    p_validate = sub.add_parser("validate-projects", help="Validate existing generated project folders.")
    p_validate.add_argument("--slug", action="append", default=[])
    p_validate.add_argument("--compiler", action="store_true")
    p_validate.add_argument("--lint", action="store_true")
    p_validate.add_argument("--test", action="store_true")
    p_validate.add_argument("--cli", action="store_true")
    p_validate.add_argument("--write-reports", action="store_true")

    p_run = sub.add_parser("run", help="Run NexusGLMHarness for selected projects, then validate.")
    p_run.add_argument("--slug", action="append", default=[])
    p_run.add_argument("--compiler", action="store_true", default=True)
    p_run.add_argument("--lint", action="store_true", default=True)
    p_run.add_argument("--test", action="store_true", default=True)
    p_run.add_argument("--cli", action="store_true", default=True)
    p_run.add_argument("--write-reports", action="store_true", default=True)

    args = parser.parse_args(argv)
    suites = load_suites(args.pack)

    if args.command == "check-specs":
        issues = [issue for suite in suites for issue in check_suite_specs(suite)]
        if issues:
            for issue in issues:
                print(f"FAIL {issue}")
            return 1
        print("PASS suite specs valid")
        return 0

    projects = select_projects_from_suites(suites, args.slug)
    if args.command == "validate-projects":
        results = [validate_project(project, args) for project in projects]
        if args.write_reports:
            write_reports(results, suites)
        print(json.dumps({"status": "pass" if all(r["status"] == "pass" for r in results) else "fail", "projects": results}, indent=2))
        return 0 if all(result["status"] == "pass" for result in results) else 1

    if args.command == "run":
        results = [run_project(project, args) for project in projects]
        if args.write_reports:
            write_reports(results, suites)
        print(json.dumps({"status": "pass" if all(r["status"] == "pass" for r in results) else "fail", "projects": results}, indent=2))
        return 0 if all(result["status"] == "pass" for result in results) else 1

    return 1


def normalize_argv(argv: list[str]) -> list[str]:
    """Allow global options before or after subcommands for copy-paste friendly use."""
    value_options = {
        "--harness-src",
        "--max-iterations",
        "--repair-attempts",
        "--max-runtime-seconds",
        "--max-model-calls",
        "--context-budget-chars",
        "--build-timeout",
        "--pack",
    }
    flag_options = {"--no-build", "--offline"}
    moved: list[str] = []
    remaining: list[str] = []
    index = 0
    while index < len(argv):
        item = argv[index]
        if item in value_options and index + 1 < len(argv):
            moved.extend([item, argv[index + 1]])
            index += 2
            continue
        if item in flag_options:
            moved.append(item)
            index += 1
            continue
        remaining.append(item)
        index += 1
    return [*moved, *remaining]


if __name__ == "__main__":
    raise SystemExit(main())
