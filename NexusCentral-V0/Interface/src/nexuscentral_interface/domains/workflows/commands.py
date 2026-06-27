import argparse
import sys

from nexuscentral_interface.domains.nexuscentral.api import emit_response, request_tool


def register_workflow_commands(subparsers: argparse._SubParsersAction) -> None:
    for name, builder in [
        ("list", _register_list),
        ("get", _register_get),
        ("save", _register_save),
        ("save-bundle", _register_save_bundle),
        ("load-bundle", _register_load_bundle),
        ("run", _register_run),
        ("run-step", _register_run_step),
        ("stop", _register_stop),
        ("get-run", _register_get_run),
        ("get-step-run", _register_get_step_run),
    ]:
        builder(subparsers, name)


def _register_list(subparsers: argparse._SubParsersAction, name: str) -> None:
    parser = subparsers.add_parser(name, help="List workflow definitions")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args: _execute_simple(args, "GET", "/nexuscentral/list_workflows"))


def _register_get(subparsers: argparse._SubParsersAction, name: str) -> None:
    parser = subparsers.add_parser(name, help="Get one workflow definition")
    parser.add_argument("--workflow-id", required=True, help="Workflow id")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args: _execute_query(args, "GET", "/nexuscentral/get_workflow", {"workflow_id": args.workflow_id}))


def _register_save(subparsers: argparse._SubParsersAction, name: str) -> None:
    parser = subparsers.add_parser(name, help="Save one workflow definition from the app catalog")
    parser.add_argument("--workflow-id", required=True, help="Workflow id")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args: _execute_payload(args, "POST", "/nexuscentral/save_workflow", {"workflow_id": args.workflow_id}))


def _register_save_bundle(subparsers: argparse._SubParsersAction, name: str) -> None:
    parser = subparsers.add_parser(name, help="Save a workflow bundle from the app catalog")
    parser.add_argument("--workflow-id", required=True, help="Workflow id")
    parser.add_argument("--bundle-id", default="", help="Optional bundle id override")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args: _execute_save_bundle(args))


def _register_load_bundle(subparsers: argparse._SubParsersAction, name: str) -> None:
    parser = subparsers.add_parser(name, help="Load a workflow bundle by id or path")
    parser.add_argument("--bundle-id", required=True, help="Workflow bundle id or path")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args: _execute_payload(args, "POST", "/nexuscentral/load_workflow_bundle", {"bundle_id": args.bundle_id}))


def _register_run(subparsers: argparse._SubParsersAction, name: str) -> None:
    parser = subparsers.add_parser(name, help="Run a workflow")
    parser.add_argument("--workflow-id", required=True, help="Workflow id")
    parser.add_argument("--input", action="append", default=[], help="Workflow input label assignment in key=value form")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args: _execute_run(args, "/nexuscentral/start_workflow_run"))


def _register_run_step(subparsers: argparse._SubParsersAction, name: str) -> None:
    parser = subparsers.add_parser(name, help="Run one workflow step")
    parser.add_argument("--workflow-id", required=True, help="Workflow id")
    parser.add_argument("--step-id", required=True, help="Step id")
    parser.add_argument("--input", action="append", default=[], help="Workflow input label assignment in key=value form")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args: _execute_run(args, "/nexuscentral/start_workflow_step"))


def _register_stop(subparsers: argparse._SubParsersAction, name: str) -> None:
    parser = subparsers.add_parser(name, help="Stop a workflow run")
    parser.add_argument("--run-id", required=True, help="Workflow run id")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args: _execute_payload(args, "POST", "/nexuscentral/stop_workflow_run", {"run_id": args.run_id}))


def _register_get_run(subparsers: argparse._SubParsersAction, name: str) -> None:
    parser = subparsers.add_parser(name, help="Inspect a workflow run")
    parser.add_argument("--run-id", required=True, help="Workflow run id")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args: _execute_query(args, "GET", "/nexuscentral/get_workflow_run", {"run_id": args.run_id}))


def _register_get_step_run(subparsers: argparse._SubParsersAction, name: str) -> None:
    parser = subparsers.add_parser(name, help="Inspect one workflow step inside a workflow run")
    parser.add_argument("--run-id", required=True, help="Workflow run id")
    parser.add_argument("--step-id", required=True, help="Workflow step id")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args: _execute_query(args, "GET", "/nexuscentral/get_workflow_step_run", {"run_id": args.run_id, "step_id": args.step_id}))


def _add_json_flag(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--json", action="store_true", help="Print the full JSON response envelope.")


def _execute_simple(args: argparse.Namespace, method: str, route: str) -> None:
    response = request_tool(method, route)
    emit_response(response, args.json)
    _exit_if_error(response)


def _execute_query(args: argparse.Namespace, method: str, route: str, query: dict) -> None:
    response = request_tool(method, route, query=query)
    emit_response(response, args.json)
    _exit_if_error(response)


def _execute_payload(args: argparse.Namespace, method: str, route: str, payload: dict) -> None:
    response = request_tool(method, route, payload=payload)
    emit_response(response, args.json)
    _exit_if_error(response)


def _execute_save_bundle(args: argparse.Namespace) -> None:
    payload = {"workflow_id": args.workflow_id}
    if args.bundle_id:
        payload["bundle_id"] = args.bundle_id
    response = request_tool("POST", "/nexuscentral/save_workflow_bundle", payload=payload)
    emit_response(response, args.json)
    _exit_if_error(response)


def _execute_run(args: argparse.Namespace, route: str) -> None:
    payload = {"workflow_id": args.workflow_id}
    inputs = _parse_inputs(args.input)
    if inputs:
        payload["input_values"] = inputs
    if hasattr(args, "step_id"):
        payload["step_id"] = args.step_id

    response = request_tool("POST", route, payload=payload)
    emit_response(response, args.json)
    _exit_if_error(response)


def _parse_inputs(raw_inputs: list[str]) -> dict:
    parsed: dict[str, str] = {}
    for item in raw_inputs:
        if "=" not in item:
            raise RuntimeError(f"Invalid --input '{item}'. Expected key=value.")
        key, value = item.split("=", 1)
        key = key.strip()
        if not key:
            raise RuntimeError(f"Invalid --input '{item}'. Key is empty.")
        parsed[key] = value
    return parsed


def _exit_if_error(response: dict) -> None:
    if not response.get("ok"):
        sys.exit(1)
