import argparse
import sys

from .api import emit_response, follow_download, request_tool


def _add_json_flag(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--json", action="store_true", help="Print the full JSON response envelope.")


def register_nexuscentral_commands(subparsers: argparse._SubParsersAction) -> None:
    for name, method, route, builder in [
        ("list-tools", "GET", "/nexuscentral/list_tools", _register_simple),
        ("status", "GET", "/nexuscentral/status", _register_simple),
        ("get-tabs", "GET", "/nexuscentral/get_tabs", _register_simple),
        ("get-current-tab", "GET", "/nexuscentral/get_current_tab", _register_simple),
        ("get-visible-elements", "GET", "/nexuscentral/get_visible_elements", _register_simple),
        ("get-form-state", "GET", "/nexuscentral/get_form_state", _register_simple),
        ("list-templates", "GET", "/nexuscentral/list_templates", _register_simple),
        ("list-wrappers", "GET", "/nexuscentral/list_wrappers", _register_simple),
        ("get-model-catalog", "GET", "/nexuscentral/get_model_catalog", _register_simple),
        ("get-runtime-inventory", "GET", "/nexuscentral/get_runtime_inventory", _register_simple),
        ("get-runtime-roles", "GET", "/nexuscentral/get_runtime_roles", _register_simple),
        ("get-template", "GET", "/nexuscentral/get_template", _register_get_template),
        ("get-wrapper", "GET", "/nexuscentral/get_wrapper", _register_get_wrapper),
        ("get-model-health", "GET", "/nexuscentral/get_model_health", _register_get_model_health),
        ("list-models", "GET", "/nexuscentral/list_models", _register_simple),
        ("get-chat-state", "GET", "/nexuscentral/get_chat_state", _register_simple),
        ("screenshot", "GET", "/nexuscentral/get_screenshot", _register_screenshot),
        ("change-tab", "POST", "/nexuscentral/change_tab", _register_change_tab),
        ("click-element", "POST", "/nexuscentral/click_element", _register_click_element),
        ("select-list-item", "POST", "/nexuscentral/select_list_item", _register_select_list_item),
        ("type-text", "POST", "/nexuscentral/type_text", _register_type_text),
        ("set-text", "POST", "/nexuscentral/set_text", _register_set_text),
        ("set-chat-tokens", "POST", "/nexuscentral/set_chat_tokens", _register_set_chat_tokens),
        ("click-button", "POST", "/nexuscentral/click_button", _register_click_button),
        ("chat-send", "POST", "/nexuscentral/chat_send", _register_chat_send),
        ("get-run-status", "GET", "/nexuscentral/get_run_status", _register_get_run_status),
        ("list-workspaces", "GET", "/nexuscentral/list_workspaces", _register_simple),
        ("select-workspace", "POST", "/nexuscentral/select_workspace", _register_select_workspace),
        ("get-workspace-tree", "GET", "/nexuscentral/get_workspace_tree", _register_get_workspace_tree),
        ("get-workspace-file", "GET", "/nexuscentral/get_workspace_file", _register_get_workspace_file),
        ("create-sandbox", "POST", "/nexuscentral/create_sandbox", _register_create_sandbox),
        ("delete-sandbox", "POST", "/nexuscentral/delete_sandbox", _register_delete_sandbox),
        ("clear-sandbox", "POST", "/nexuscentral/clear_sandbox", _register_clear_sandbox),
        ("save-sandbox-file", "POST", "/nexuscentral/save_sandbox_file", _register_save_sandbox_file),
        ("import-wrapper", "POST", "/nexuscentral/import_wrapper", _register_import_wrapper),
        ("validate-wrapper", "POST", "/nexuscentral/validate_wrapper", _register_validate_wrapper),
        ("run-wrapper", "POST", "/nexuscentral/run_wrapper", _register_run_wrapper),
        ("get-wrapper-run", "GET", "/nexuscentral/get_wrapper_run", _register_get_wrapper_run),
        ("cancel-wrapper-run", "POST", "/nexuscentral/cancel_wrapper_run", _register_cancel_wrapper_run),
        ("get-wrapper-run-artifacts", "GET", "/nexuscentral/get_wrapper_run_artifacts", _register_get_wrapper_run_artifacts),
        ("apply-template-to-chat", "POST", "/nexuscentral/apply_template_to_chat", _register_apply_template_to_chat),
        ("import-local-model", "POST", "/nexuscentral/import_local_model", _register_import_local_model),
        ("verify-model", "POST", "/nexuscentral/verify_model", _register_model_id_payload),
        ("repair-model", "POST", "/nexuscentral/repair_model", _register_model_id_payload),
        ("quarantine-model", "POST", "/nexuscentral/quarantine_model", _register_quarantine_model),
        ("preload-model", "POST", "/nexuscentral/preload_model", _register_model_id_payload),
        ("pin-model", "POST", "/nexuscentral/pin_model", _register_model_id_payload),
        ("unpin-model", "POST", "/nexuscentral/unpin_model", _register_model_id_payload),
        ("activate-local-model", "POST", "/nexuscentral/activate_local_model", _register_activate_local_model),
        ("activate-instance", "POST", "/nexuscentral/activate_instance", _register_instance_id_payload),
        ("deactivate-local-model", "POST", "/nexuscentral/deactivate_local_model", _register_instance_id_payload),
        ("reload-model", "POST", "/nexuscentral/reload_model", _register_instance_id_payload),
        ("delete-instance", "POST", "/nexuscentral/delete_instance", _register_instance_id_payload),
        ("set-instance-mode", "POST", "/nexuscentral/set_instance_mode", _register_set_instance_mode),
        ("set-instance-tokens", "POST", "/nexuscentral/set_instance_tokens", _register_set_instance_tokens),
        ("set-instance-memory-capacity", "POST", "/nexuscentral/set_instance_memory_capacity", _register_set_instance_memory_capacity),
        ("get-instance-console", "GET", "/nexuscentral/get_instance_console", _register_get_instance_console),
        ("set-runtime-roles", "POST", "/nexuscentral/set_runtime_roles", _register_set_runtime_roles),
        ("pull-model", "POST", "/nexuscentral/pull_model", _register_pull_model),
        ("get-logs", "GET", "/nexuscentral/get_logs", _register_get_logs),
        ("instruct", "POST", "/nexuscentral/instruct", _register_thinking),
        ("thinking", "POST", "/nexuscentral/thinking", _register_thinking),
    ]:
        builder(subparsers, name, method, route)


def _register_simple(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help=f"Call {route}")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_simple(args, m, r))


def _register_clear_sandbox(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Clear the NexusCentral sandbox workspace")
    parser.add_argument("--workspace-id", default="", help="Optional sandbox workspace id; defaults to built-in sandbox")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_payload(args, m, r, {"workspace_id": args.workspace_id} if args.workspace_id else {}))


def _register_create_sandbox(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Create and select a new NexusCentral sandbox workspace")
    parser.add_argument("--name", default="", help="Optional sandbox display name")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_payload(args, m, r, {"name": args.name} if args.name else {}))


def _register_delete_sandbox(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Delete a non-default NexusCentral sandbox workspace")
    parser.add_argument("--workspace-id", required=True, help="Sandbox workspace id")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_payload(args, m, r, {"workspace_id": args.workspace_id}))


def _register_select_workspace(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Select the active Lab workspace")
    parser.add_argument("--workspace-id", required=True, help="Workspace id")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_payload(args, m, r, {"workspace_id": args.workspace_id}))


def _register_get_workspace_tree(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Get a Lab workspace file tree")
    parser.add_argument("--workspace-id", default="", help="Optional workspace id")
    parser.add_argument("--directory-path", default="", help="Optional absolute or workspace-relative directory path")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_workspace_tree(args, m, r))


def _register_get_workspace_file(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Read a file from a Lab workspace")
    parser.add_argument("--workspace-id", default="", help="Optional workspace id")
    parser.add_argument("--path", required=True, help="Absolute or workspace-relative file path")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_workspace_file(args, m, r))


def _register_save_sandbox_file(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Save a file inside a sandbox workspace")
    parser.add_argument("--workspace-id", default="", help="Optional sandbox workspace id")
    parser.add_argument("--path", required=True, help="Workspace-relative file path")
    parser.add_argument("--text", default="", help="Text to write")
    parser.add_argument("--text-file", default="", help="Read text to write from this file")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_save_sandbox_file(args, m, r))


def _register_change_tab(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Change the active desktop tab")
    parser.add_argument("--tab", required=True, help="Target tab name")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_payload(args, m, r, {"tab": args.tab}))


def _register_click_element(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Perform a semantic click on a UI element")
    parser.add_argument("--element-id", required=True, help="Semantic UI element id")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_payload(args, m, r, {"element_id": args.element_id}))


def _register_select_list_item(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Select a semantic list or dropdown option")
    parser.add_argument("--element-id", required=True, help="Semantic parent element id")
    parser.add_argument("--item-id", default="", help="Stable option id")
    parser.add_argument("--label", default="", help="Visible option label")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_select_list_item(args, m, r))


def _register_set_text(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Set text in a semantic text input")
    parser.add_argument("--element-id", default="chat.input_box", help="Semantic input element id")
    parser.add_argument("--text", required=True, help="Text to place into the input")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_payload(args, m, r, {"element_id": args.element_id, "text": args.text}))


def _register_type_text(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Type text into a semantic text input by appending to its current value")
    parser.add_argument("--element-id", default="chat.input_box", help="Semantic input element id")
    parser.add_argument("--text", required=True, help="Text to append into the input")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_payload(args, m, r, {"element_id": args.element_id, "text": args.text}))


def _register_set_chat_tokens(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Set the chat max-tokens slider value")
    parser.add_argument("--value", required=True, type=int, help="Token count between 100 and 5000")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_payload(args, m, r, {"value": args.value}))


def _register_click_button(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Activate a semantic button")
    parser.add_argument("--element-id", required=True, help="Semantic button element id")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_payload(args, m, r, {"element_id": args.element_id}))


def _register_chat_send(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Send a chat prompt through the live app chat surface")
    parser.add_argument("--text", required=True, help="Prompt text")
    parser.add_argument("--model-id", default="", help="Optional model id override")
    parser.add_argument("--execution-mode", default="", choices=("direct_llm", "wrapper"), help="Optional execution mode override")
    parser.add_argument("--template-id", default="", help="Optional template id override")
    parser.add_argument("--wrapper-id", default="", help="Optional wrapper id override")
    parser.add_argument("--artifact-input", action="append", default=[], help="Artifact input path; may be repeated")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_chat_send(args, m, r))


def _register_get_run_status(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Inspect the status of a chat run")
    parser.add_argument("--run-id", required=True, help="Run id returned by chat-send or click-button")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_get_run_status(args, m, r))


def _register_get_model_health(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Inspect persisted model health")
    parser.add_argument("--model-id", required=True, help="Catalog model id")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_get_model_health(args, m, r))


def _register_get_template(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Inspect one template definition")
    parser.add_argument("--template-id", required=True, help="Template id")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_simple_query(args, m, r, {"template_id": args.template_id}))


def _register_get_wrapper(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Inspect one wrapper definition")
    parser.add_argument("--wrapper-id", required=True, help="Wrapper id")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_simple_query(args, m, r, {"wrapper_id": args.wrapper_id}))


def _register_validate_wrapper(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Validate a wrapper")
    parser.add_argument("--wrapper-id", required=True, help="Wrapper id")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_payload(args, m, r, {"wrapper_id": args.wrapper_id}))


def _register_import_wrapper(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Import an external wrapper manifest")
    parser.add_argument("--path", required=True, help="Path to a wrapper manifest inside an external build folder")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_payload(args, m, r, {"path": args.path}))


def _register_run_wrapper(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Run a wrapper task outside Chat")
    parser.add_argument("--template-id", required=True, help="Template id")
    parser.add_argument("--prompt", required=True, help="Prompt text")
    parser.add_argument("--wrapper-id", default="", help="Optional wrapper id override")
    parser.add_argument("--model-id", default="", help="Optional model id")
    parser.add_argument("--session-id", default="", help="Optional session id override")
    parser.add_argument("--max-tokens", type=int, default=1200, help="Maximum tokens")
    parser.add_argument("--artifact-input", action="append", default=[], help="Artifact input path; may be repeated")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_run_wrapper(args, m, r))


def _register_get_wrapper_run(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Inspect a wrapper run")
    parser.add_argument("--run-id", required=True, help="Wrapper run id")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_simple_query(args, m, r, {"run_id": args.run_id}))


def _register_cancel_wrapper_run(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Cancel a running wrapper task")
    parser.add_argument("--run-id", required=True, help="Wrapper run id")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_payload(args, m, r, {"run_id": args.run_id}))


def _register_get_wrapper_run_artifacts(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="List artifacts from a wrapper run")
    parser.add_argument("--run-id", required=True, help="Wrapper run id")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_simple_query(args, m, r, {"run_id": args.run_id}))


def _register_apply_template_to_chat(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Apply a template to the live Chat view")
    parser.add_argument("--template-id", required=True, help="Template id")
    parser.add_argument("--wrapper-id", default="", help="Optional wrapper id override")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_apply_template(args, m, r))


def _register_import_local_model(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Import a local model file into the app catalog")
    parser.add_argument("--path", required=True, help="Path to a local GGUF file")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_payload(args, m, r, {"path": args.path}))


def _register_model_id_payload(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help=f"Call {route} for a model")
    parser.add_argument("--model-id", required=True, help="Catalog model id")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_payload(args, m, r, {"model_id": args.model_id}))


def _register_quarantine_model(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Quarantine a model explicitly")
    parser.add_argument("--model-id", required=True, help="Catalog model id")
    parser.add_argument("--reason", default="", help="Optional quarantine reason")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_payload(
        args,
        m,
        r,
        {"model_id": args.model_id, "reason": args.reason} if args.reason else {"model_id": args.model_id},
    ))


def _register_activate_local_model(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Activate a local model into a runtime role")
    parser.add_argument("--model-id", required=True, help="Catalog model id")
    parser.add_argument("--role", default="thinking", help="Target role name")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_payload(args, m, r, {"model_id": args.model_id, "role": args.role}))


def _register_instance_id_payload(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help=f"Call {route} for a loaded instance")
    parser.add_argument("--instance-id", required=True, help="Loaded instance id")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_payload(args, m, r, {"instance_id": args.instance_id}))


def _register_set_instance_tokens(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Set the token budget for a loaded instance")
    parser.add_argument("--instance-id", required=True, help="Loaded instance id")
    parser.add_argument("--value", required=True, type=int, help="Token count")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_payload(args, m, r, {"instance_id": args.instance_id, "value": args.value}))


def _register_set_instance_memory_capacity(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Set the endpoint memory capacity for a loaded instance")
    parser.add_argument("--instance-id", required=True, help="Loaded instance id")
    parser.add_argument("--value", required=True, type=int, help="Memory capacity in MB")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_payload(args, m, r, {"instance_id": args.instance_id, "value": args.value}))


def _register_set_instance_mode(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Set the primary mode for a loaded instance")
    parser.add_argument("--instance-id", required=True, help="Loaded instance id")
    parser.add_argument("--mode", required=True, choices=("thinking", "instruct"), help="Primary instance mode")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_payload(args, m, r, {"instance_id": args.instance_id, "mode": args.mode}))


def _register_get_instance_console(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Fetch recent console lines for a loaded instance")
    parser.add_argument("--instance-id", required=True, help="Loaded instance id")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_simple_query(args, m, r, {"instance_id": args.instance_id}))


def _register_set_runtime_roles(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Assign or tune a runtime role")
    parser.add_argument("--role", required=True, help="Role name such as instruct or thinking")
    parser.add_argument("--model-id", default="", help="Optional assigned model id")
    parser.add_argument("--routing-mode", default="", help="Optional routing mode")
    parser.add_argument("--reserved-memory", type=int, default=None, help="Optional reserved memory")
    parser.add_argument("--max-parallel", type=int, default=None, help="Optional max parallel")
    parser.add_argument("--queue-limit", type=int, default=None, help="Optional queue limit")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_set_runtime_roles(args, m, r))


def _register_pull_model(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Start a model download through the app integration")
    parser.add_argument("--repo", required=True, help="Hugging Face repo id")
    parser.add_argument("--filename", required=True, help="Artifact filename")
    parser.add_argument("--revision", default="main", help="Repo revision")
    parser.add_argument("--follow", action="store_true", help="Follow verbose download logs until completion")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_pull_model(args, m, r))


def _register_get_logs(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Fetch integration or download logs")
    parser.add_argument("--category", default="", help="Optional log category")
    parser.add_argument("--download-id", default="", help="Optional download id")
    parser.add_argument("--limit", default="100", help="Maximum lines/events")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_get_logs(args, m, r))


def _register_thinking(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Run a prompt through the packaged thinking runtime")
    parser.add_argument("--prompt", required=True, help="Prompt text")
    parser.add_argument("--model-id", default="", help="Optional catalog model id")
    parser.add_argument("--instance-id", default="", help="Optional loaded instance id")
    parser.add_argument("--max-tokens", type=int, default=1200, help="Maximum tokens to request")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_thinking(args, m, r))


def _register_screenshot(subparsers: argparse._SubParsersAction, name: str, method: str, route: str) -> None:
    parser = subparsers.add_parser(name, help="Capture a screenshot through the app route")
    parser.add_argument("--out", required=True, help="Destination PNG file")
    _add_json_flag(parser)
    parser.set_defaults(func=lambda args, m=method, r=route: _execute_screenshot(args, m, r))


def _execute_simple(args: argparse.Namespace, method: str, route: str) -> None:
    response = request_tool(method, route)
    emit_response(response, args.json)
    _exit_if_error(response)


def _execute_simple_query(args: argparse.Namespace, method: str, route: str, query: dict) -> None:
    response = request_tool(method, route, query=query)
    emit_response(response, args.json)
    _exit_if_error(response)


def _execute_payload(args: argparse.Namespace, method: str, route: str, payload: dict) -> None:
    response = request_tool(method, route, payload=payload)
    emit_response(response, args.json)
    _exit_if_error(response)


def _execute_select_list_item(args: argparse.Namespace, method: str, route: str) -> None:
    if not args.item_id and not args.label:
        raise RuntimeError("select-list-item requires either --item-id or --label.")

    payload = {"element_id": args.element_id}
    if args.item_id:
        payload["item_id"] = args.item_id
    if args.label:
        payload["label"] = args.label

    response = request_tool(method, route, payload=payload)
    emit_response(response, args.json)
    _exit_if_error(response)


def _execute_workspace_tree(args: argparse.Namespace, method: str, route: str) -> None:
    query = {}
    if args.workspace_id:
        query["workspace_id"] = args.workspace_id
    if args.directory_path:
        query["directory_path"] = args.directory_path
    response = request_tool(method, route, query=query)
    emit_response(response, args.json)
    _exit_if_error(response)


def _execute_workspace_file(args: argparse.Namespace, method: str, route: str) -> None:
    query = {"path": args.path}
    if args.workspace_id:
        query["workspace_id"] = args.workspace_id
    response = request_tool(method, route, query=query)
    emit_response(response, args.json)
    _exit_if_error(response)


def _execute_save_sandbox_file(args: argparse.Namespace, method: str, route: str) -> None:
    if args.text_file:
        with open(args.text_file, "r", encoding="utf-8") as handle:
            text = handle.read()
    else:
        text = args.text
    payload = {"path": args.path, "text": text}
    if args.workspace_id:
        payload["workspace_id"] = args.workspace_id
    response = request_tool(method, route, payload=payload)
    emit_response(response, args.json)
    _exit_if_error(response)


def _execute_pull_model(args: argparse.Namespace, method: str, route: str) -> None:
    response = request_tool(
        method,
        route,
        payload={"repo": args.repo, "filename": args.filename, "revision": args.revision},
    )
    emit_response(response, args.json)
    _exit_if_error(response)

    if args.follow:
        download_id = response.get("data", {}).get("download_id")
        if not download_id:
            raise RuntimeError("pull_model did not return a download_id.")
        final_logs = follow_download(download_id, args.json)
        if args.json:
            print("")
            emit_response(final_logs, True)


def _execute_thinking(args: argparse.Namespace, method: str, route: str) -> None:
    payload = {"prompt": args.prompt, "max_tokens": args.max_tokens}
    if args.model_id:
        payload["model_id"] = args.model_id
    if args.instance_id:
        payload["instance_id"] = args.instance_id
    response = request_tool(method, route, payload=payload)
    emit_response(response, args.json)
    _exit_if_error(response)


def _execute_chat_send(args: argparse.Namespace, method: str, route: str) -> None:
    payload = {"text": args.text}
    if args.model_id:
        payload["model_id"] = args.model_id
    if args.execution_mode:
        payload["execution_mode"] = args.execution_mode
    if args.template_id:
        payload["template_id"] = args.template_id
    if args.wrapper_id:
        payload["wrapper_id"] = args.wrapper_id
    if args.artifact_input:
        payload["artifact_inputs"] = list(args.artifact_input)
    response = request_tool(method, route, payload=payload)
    emit_response(response, args.json)
    _exit_if_error(response)


def _execute_run_wrapper(args: argparse.Namespace, method: str, route: str) -> None:
    payload = {
        "template_id": args.template_id,
        "prompt": args.prompt,
        "max_tokens": args.max_tokens,
    }
    if args.wrapper_id:
        payload["wrapper_id"] = args.wrapper_id
    if args.model_id:
        payload["model_id"] = args.model_id
    if args.session_id:
        payload["session_id"] = args.session_id
    if args.artifact_input:
        payload["artifact_inputs"] = list(args.artifact_input)
    response = request_tool(method, route, payload=payload)
    emit_response(response, args.json)
    _exit_if_error(response)


def _execute_apply_template(args: argparse.Namespace, method: str, route: str) -> None:
    payload = {"template_id": args.template_id}
    if args.wrapper_id:
        payload["wrapper_id"] = args.wrapper_id
    response = request_tool(method, route, payload=payload)
    emit_response(response, args.json)
    _exit_if_error(response)


def _execute_set_runtime_roles(args: argparse.Namespace, method: str, route: str) -> None:
    payload = {"role": args.role}
    if args.model_id:
        payload["model_id"] = args.model_id
    if args.routing_mode:
        payload["routing_mode"] = args.routing_mode
    if args.reserved_memory is not None:
        payload["reserved_memory"] = args.reserved_memory
    if args.max_parallel is not None:
        payload["max_parallel"] = args.max_parallel
    if args.queue_limit is not None:
        payload["queue_limit"] = args.queue_limit
    response = request_tool(method, route, payload=payload)
    emit_response(response, args.json)
    _exit_if_error(response)


def _execute_get_logs(args: argparse.Namespace, method: str, route: str) -> None:
    query = {"limit": args.limit}
    if args.category:
        query["category"] = args.category
    if args.download_id:
        query["download_id"] = args.download_id
    response = request_tool(method, route, query=query)
    emit_response(response, args.json)
    _exit_if_error(response)


def _execute_get_run_status(args: argparse.Namespace, method: str, route: str) -> None:
    response = request_tool(method, route, query={"run_id": args.run_id})
    emit_response(response, args.json)
    _exit_if_error(response)


def _execute_get_model_health(args: argparse.Namespace, method: str, route: str) -> None:
    response = request_tool(method, route, query={"model_id": args.model_id})
    emit_response(response, args.json)
    _exit_if_error(response)


def _execute_screenshot(args: argparse.Namespace, method: str, route: str) -> None:
    import base64
    import os

    response = request_tool(method, route)
    _exit_if_error(response)

    image_b64 = response.get("data", {}).get("image")
    if not image_b64:
        raise RuntimeError("Screenshot response did not include image data.")

    out_dir = os.path.dirname(args.out)
    if out_dir:
        os.makedirs(out_dir, exist_ok=True)
    with open(args.out, "wb") as handle:
        handle.write(base64.b64decode(image_b64))

    if args.json:
        emit_response(response, True)
    else:
        print(f"Screenshot saved to {args.out}")


def _exit_if_error(response: dict) -> None:
    if not response.get("ok"):
        sys.exit(1)
