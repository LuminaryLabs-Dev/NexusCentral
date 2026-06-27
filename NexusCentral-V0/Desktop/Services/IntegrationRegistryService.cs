using System.Collections.Generic;
using System.Linq;
using NexusCentral_Avalonia.Models;

namespace NexusCentral_Avalonia.Services;

public class IntegrationRegistryService
{
    private readonly IReadOnlyList<IntegrationDefinition> _integrations;

    public IntegrationRegistryService()
    {
        var nexuscentralTools = new List<ToolDefinition>
        {
            new("list_tools", "/nexuscentral/list_tools", "GET", "List discoverable NexusCentral tools.", "read", true, false, new Dictionary<string, string>()),
            new("status", "/nexuscentral/status", "GET", "Get the overall supervisor and runtime status.", "read", true, false, new Dictionary<string, string>()),
            new("get_tabs", "/nexuscentral/get_tabs", "GET", "Get the visible top-level tabs.", "read", true, false, new Dictionary<string, string>()),
            new("get_current_tab", "/nexuscentral/get_current_tab", "GET", "Get the currently selected tab.", "read", true, false, new Dictionary<string, string>()),
            new("change_tab", "/nexuscentral/change_tab", "POST", "Change the active tab in the desktop UI.", "write", true, false, new Dictionary<string, string> { ["tab"] = "Target tab name." }),
            new("get_visible_elements", "/nexuscentral/get_visible_elements", "GET", "Return visible semantic UI elements and their highlight state.", "read", true, false, new Dictionary<string, string>()),
            new("get_form_state", "/nexuscentral/get_form_state", "GET", "Return semantic form state for the currently active view.", "read", true, false, new Dictionary<string, string>()),
            new("click_element", "/nexuscentral/click_element", "POST", "Perform a semantic click action on a registered UI element.", "write", true, false, new Dictionary<string, string> { ["element_id"] = "Semantic UI element id." }),
            new("select_list_item", "/nexuscentral/select_list_item", "POST", "Select a semantic dropdown or list item by id or label.", "write", true, false, new Dictionary<string, string> { ["element_id"] = "Semantic parent element id.", ["item_id"] = "Optional item id.", ["label"] = "Optional visible label." }),
            new("screenshot", "/nexuscentral/screenshot", "GET", "Capture a screenshot of the running desktop window for human-view validation.", "read", true, false, new Dictionary<string, string>()),
            new("get_screenshot", "/nexuscentral/get_screenshot", "GET", "Legacy alias for screenshot; captures the running desktop window.", "read", true, false, new Dictionary<string, string>()),
            new("list_templates", "/nexuscentral/list_templates", "GET", "List registered chat and workflow templates.", "read", true, false, new Dictionary<string, string>()),
            new("get_template", "/nexuscentral/get_template", "GET", "Return one template definition by id.", "read", true, false, new Dictionary<string, string> { ["template_id"] = "Template id." }),
            new("list_workflows", "/nexuscentral/list_workflows", "GET", "List registered workflow definitions.", "read", true, false, new Dictionary<string, string>()),
            new("get_workflow", "/nexuscentral/get_workflow", "GET", "Return one workflow definition by id.", "read", true, false, new Dictionary<string, string> { ["workflow_id"] = "Workflow id." }),
            new("save_workflow", "/nexuscentral/save_workflow", "POST", "Save a workflow definition to the user workflow store.", "write", true, false, new Dictionary<string, string> { ["workflow"] = "Workflow definition payload." }),
            new("load_workflow_bundle", "/nexuscentral/load_workflow_bundle", "POST", "Load a saved workflow editor bundle by id or path.", "read", true, false, new Dictionary<string, string> { ["bundle_id"] = "Workflow bundle id or path." }),
            new("save_workflow_bundle", "/nexuscentral/save_workflow_bundle", "POST", "Save a workflow editor bundle and last label values.", "write", true, false, new Dictionary<string, string> { ["bundle"] = "Workflow bundle payload.", ["bundle_id"] = "Optional workflow bundle id." }),
            new("start_workflow_run", "/nexuscentral/start_workflow_run", "POST", "Run a workflow sequentially across its enabled steps.", "write", true, false, new Dictionary<string, string> { ["workflow_id"] = "Workflow id or inline workflow payload.", ["input_values"] = "Optional workflow input values." }),
            new("start_workflow_step", "/nexuscentral/start_workflow_step", "POST", "Run a single workflow step in isolation.", "write", true, false, new Dictionary<string, string> { ["workflow_id"] = "Workflow id or inline workflow payload.", ["step_id"] = "Workflow step id.", ["input_values"] = "Optional workflow input values." }),
            new("stop_workflow_run", "/nexuscentral/stop_workflow_run", "POST", "Stop a running workflow execution.", "write", true, false, new Dictionary<string, string> { ["run_id"] = "Workflow run id." }),
            new("get_workflow_run", "/nexuscentral/get_workflow_run", "GET", "Inspect a workflow run by id.", "read", true, false, new Dictionary<string, string> { ["run_id"] = "Workflow run id." }),
            new("get_workflow_step_run", "/nexuscentral/get_workflow_step_run", "GET", "Inspect one workflow step run inside a workflow run.", "read", true, false, new Dictionary<string, string> { ["run_id"] = "Workflow run id.", ["step_id"] = "Workflow step id." }),
            new("list_wrappers", "/nexuscentral/list_wrappers", "GET", "List registered wrapper runtimes.", "read", true, false, new Dictionary<string, string>()),
            new("get_wrapper", "/nexuscentral/get_wrapper", "GET", "Return one wrapper definition by id.", "read", true, false, new Dictionary<string, string> { ["wrapper_id"] = "Wrapper id." }),
            new("import_wrapper", "/nexuscentral/import_wrapper", "POST", "Import an external wrapper manifest into the local wrapper store.", "write", true, false, new Dictionary<string, string> { ["path"] = "Path to a wrapper manifest inside an external build folder." }),
            new("validate_wrapper", "/nexuscentral/validate_wrapper", "POST", "Validate a wrapper command and its NexusCentral tooling access.", "write", true, false, new Dictionary<string, string> { ["wrapper_id"] = "Wrapper id." }),
            new("run_wrapper", "/nexuscentral/run_wrapper", "POST", "Run a wrapper task outside the chat UI.", "write", true, false, new Dictionary<string, string> { ["template_id"] = "Template id.", ["wrapper_id"] = "Wrapper id.", ["prompt"] = "Prompt text." }),
            new("get_wrapper_run", "/nexuscentral/get_wrapper_run", "GET", "Inspect a wrapper run by id.", "read", true, false, new Dictionary<string, string> { ["run_id"] = "Wrapper run id." }),
            new("cancel_wrapper_run", "/nexuscentral/cancel_wrapper_run", "POST", "Cancel a running wrapper task.", "write", true, false, new Dictionary<string, string> { ["run_id"] = "Wrapper run id." }),
            new("get_wrapper_run_artifacts", "/nexuscentral/get_wrapper_run_artifacts", "GET", "List artifact outputs for a wrapper run.", "read", true, false, new Dictionary<string, string> { ["run_id"] = "Wrapper run id." }),
            new("apply_template_to_chat", "/nexuscentral/apply_template_to_chat", "POST", "Apply a template and optional wrapper selection to Chat.", "write", true, false, new Dictionary<string, string> { ["template_id"] = "Template id.", ["wrapper_id"] = "Optional wrapper id." }),
            new("list_models", "/nexuscentral/list_models", "GET", "List catalog model ids and display names.", "read", true, false, new Dictionary<string, string>()),
            new("get_model_catalog", "/nexuscentral/get_model_catalog", "GET", "Return the detailed local model catalog.", "read", true, false, new Dictionary<string, string>()),
            new("get_runtime_inventory", "/nexuscentral/get_runtime_inventory", "GET", "Return library-backed runtime inventory for loaded models.", "read", true, false, new Dictionary<string, string>()),
            new("get_runtime_roles", "/nexuscentral/get_runtime_roles", "GET", "Return runtime endpoint role assignments.", "read", true, false, new Dictionary<string, string>()),
            new("set_runtime_roles", "/nexuscentral/set_runtime_roles", "POST", "Assign or update a runtime endpoint role.", "write", true, false, new Dictionary<string, string> { ["role"] = "Endpoint role name.", ["model_id"] = "Assigned model id.", ["routing_mode"] = "Optional routing mode." }),
            new("get_model_health", "/nexuscentral/get_model_health", "GET", "Return persisted health details for a model.", "read", true, false, new Dictionary<string, string> { ["model_id"] = "Catalog model id." }),
            new("import_local_model", "/nexuscentral/import_local_model", "POST", "Register a local GGUF model into the catalog.", "write", true, false, new Dictionary<string, string> { ["path"] = "Absolute or bundle-relative path to the model file." }),
            new("verify_model", "/nexuscentral/verify_model", "POST", "Verify a model file and quarantine it if unhealthy.", "write", true, false, new Dictionary<string, string> { ["model_id"] = "Catalog model id." }),
            new("repair_model", "/nexuscentral/repair_model", "POST", "Repair a model by re-downloading or re-importing it using stored metadata when available.", "write", true, false, new Dictionary<string, string> { ["model_id"] = "Catalog model id." }),
            new("quarantine_model", "/nexuscentral/quarantine_model", "POST", "Force-quarantine a model and block it from runtime use.", "write", true, false, new Dictionary<string, string> { ["model_id"] = "Catalog model id.", ["reason"] = "Optional quarantine reason." }),
            new("preload_model", "/nexuscentral/preload_model", "POST", "Load a model into the library asynchronously.", "write", true, false, new Dictionary<string, string> { ["model_id"] = "Catalog model id." }),
            new("pin_model", "/nexuscentral/pin_model", "POST", "Pin a model in the library.", "write", true, false, new Dictionary<string, string> { ["model_id"] = "Catalog model id." }),
            new("unpin_model", "/nexuscentral/unpin_model", "POST", "Unpin a model in the library.", "write", true, false, new Dictionary<string, string> { ["model_id"] = "Catalog model id." }),
            new("activate_local_model", "/nexuscentral/activate_local_model", "POST", "Preload a local model and assign it to a target endpoint role.", "write", true, false, new Dictionary<string, string> { ["model_id"] = "Catalog model id.", ["role"] = "Target endpoint role." }),
            new("activate_instance", "/nexuscentral/activate_instance", "POST", "Activate an existing loaded instance card.", "write", true, false, new Dictionary<string, string> { ["instance_id"] = "Loaded instance id." }),
            new("deactivate_local_model", "/nexuscentral/deactivate_local_model", "POST", "Deactivate a loaded instance.", "write", true, false, new Dictionary<string, string> { ["instance_id"] = "Loaded instance id." }),
            new("reload_model", "/nexuscentral/reload_model", "POST", "Reload a loaded instance.", "write", true, false, new Dictionary<string, string> { ["instance_id"] = "Loaded instance id." }),
            new("delete_instance", "/nexuscentral/delete_instance", "POST", "Delete a loaded instance card.", "write", true, false, new Dictionary<string, string> { ["instance_id"] = "Loaded instance id." }),
            new("set_instance_mode", "/nexuscentral/set_instance_mode", "POST", "Set the primary mode for a loaded instance.", "write", true, false, new Dictionary<string, string> { ["instance_id"] = "Loaded instance id.", ["mode"] = "Mode such as thinking or instruct." }),
            new("set_instance_tokens", "/nexuscentral/set_instance_tokens", "POST", "Set the token budget for a loaded instance.", "write", true, false, new Dictionary<string, string> { ["instance_id"] = "Loaded instance id.", ["value"] = "Token count between 100 and 5000." }),
            new("set_instance_memory_capacity", "/nexuscentral/set_instance_memory_capacity", "POST", "Set endpoint memory capacity for a loaded instance.", "write", true, false, new Dictionary<string, string> { ["instance_id"] = "Loaded instance id.", ["value"] = "Memory capacity in MB between 50 and 2048." }),
            new("get_instance_console", "/nexuscentral/get_instance_console", "GET", "Return recent console output for a loaded instance.", "read", true, false, new Dictionary<string, string> { ["instance_id"] = "Loaded instance id." }),
            new("pull_model", "/nexuscentral/pull_model", "POST", "Download a model into the shared local model library.", "write", true, false, new Dictionary<string, string> { ["repo"] = "Hugging Face repo id.", ["filename"] = "Artifact filename.", ["revision"] = "Optional branch or tag." }),
            new("get_logs", "/nexuscentral/get_logs", "GET", "Fetch recent logs, optionally filtered by category or download id.", "read", true, false, new Dictionary<string, string> { ["category"] = "Optional log category.", ["download_id"] = "Optional download job id." }),
            new("instruct", "/nexuscentral/instruct", "POST", "Run one prompt against the model assigned to /instruct.", "write", true, false, new Dictionary<string, string> { ["prompt"] = "Prompt text.", ["model_id"] = "Optional explicit model id." }),
            new("thinking", "/nexuscentral/thinking", "POST", "Run one prompt against the packaged runtime sidecar and return only the final answer.", "write", true, false, new Dictionary<string, string> { ["prompt"] = "Prompt text.", ["model_id"] = "Optional catalog model id.", ["instance_id"] = "Optional loaded instance id.", ["max_tokens"] = "Optional token cap." }),
            new("thinking-raw", "/nexuscentral/thinking-raw", "POST", "Run one prompt against the packaged runtime sidecar and return the full raw output including thinking blocks.", "write", true, false, new Dictionary<string, string> { ["prompt"] = "Prompt text.", ["model_id"] = "Optional catalog model id.", ["instance_id"] = "Optional loaded instance id.", ["max_tokens"] = "Optional token cap." }),
            new("thinking-stream", "/nexuscentral/thinking-stream", "POST", "Run one thinking prompt and stream runtime chunks as NDJSON before the final raw output.", "write", true, false, new Dictionary<string, string> { ["prompt"] = "Prompt text.", ["model_id"] = "Optional catalog model id.", ["instance_id"] = "Optional loaded instance id.", ["max_tokens"] = "Optional token cap." }),
            new("get_chat_state", "/nexuscentral/get_chat_state", "GET", "Inspect the current chat session, selection, and generation state.", "read", true, false, new Dictionary<string, string>()),
            new("get_lab_bootstrap", "/nexuscentral/get_lab_bootstrap", "GET", "Return browser-ready Nexus Web Lab bootstrap state.", "read", true, false, new Dictionary<string, string>()),
            new("get_lab_state", "/nexuscentral/get_lab_state", "GET", "Return the current Nexus Web Lab state snapshot.", "read", true, false, new Dictionary<string, string>()),
            new("list_workspaces", "/nexuscentral/list_workspaces", "GET", "List Lab workspaces including Sandbox.", "read", true, false, new Dictionary<string, string>()),
            new("select_workspace", "/nexuscentral/select_workspace", "POST", "Select the active Lab workspace by id.", "write", true, false, new Dictionary<string, string> { ["workspace_id"] = "Workspace id." }),
            new("open_workspace", "/nexuscentral/open_workspace", "POST", "Open a directory-backed workspace in Lab.", "write", true, false, new Dictionary<string, string> { ["path"] = "Path to a workspace root directory." }),
            new("get_workspace_tree", "/nexuscentral/get_workspace_tree", "GET", "Get the file tree for a Lab workspace path.", "read", true, false, new Dictionary<string, string> { ["workspace_id"] = "Workspace id.", ["directory_path"] = "Optional directory path inside the workspace." }),
            new("get_workspace_file", "/nexuscentral/get_workspace_file", "GET", "Read a file from the selected Lab workspace.", "read", true, false, new Dictionary<string, string> { ["workspace_id"] = "Workspace id.", ["path"] = "Absolute or workspace-relative file path." }),
            new("create_sandbox", "/nexuscentral/create_sandbox", "POST", "Create and select a new named sandbox workspace.", "write", true, false, new Dictionary<string, string> { ["name"] = "Optional sandbox display name." }),
            new("delete_sandbox", "/nexuscentral/delete_sandbox", "POST", "Delete a non-default sandbox workspace and its temp storage.", "write", true, false, new Dictionary<string, string> { ["workspace_id"] = "Sandbox workspace id." }),
            new("save_sandbox_file", "/nexuscentral/save_sandbox_file", "POST", "Save a file inside the sandbox workspace.", "write", true, false, new Dictionary<string, string> { ["workspace_id"] = "Sandbox workspace id.", ["path"] = "File path.", ["text"] = "File contents." }),
            new("delete_sandbox_path", "/nexuscentral/delete_sandbox_path", "POST", "Delete a file or directory inside the sandbox workspace.", "write", true, false, new Dictionary<string, string> { ["workspace_id"] = "Sandbox workspace id.", ["path"] = "Path to delete." }),
            new("clear_sandbox", "/nexuscentral/clear_sandbox", "POST", "Clear all children inside the sandbox workspace while preserving the sandbox root.", "write", true, false, new Dictionary<string, string> { ["workspace_id"] = "Optional sandbox workspace id; defaults to the built-in sandbox." }),
            new("create_sandbox_directory", "/nexuscentral/create_sandbox_directory", "POST", "Create a directory inside the sandbox workspace.", "write", true, false, new Dictionary<string, string> { ["workspace_id"] = "Sandbox workspace id.", ["path"] = "Directory path." }),
            new("rename_sandbox_path", "/nexuscentral/rename_sandbox_path", "POST", "Rename a file or directory inside the sandbox workspace.", "write", true, false, new Dictionary<string, string> { ["workspace_id"] = "Sandbox workspace id.", ["source_path"] = "Current path.", ["destination_path"] = "New path." }),
            new("save_sandbox_to_directory", "/nexuscentral/save_sandbox_to_directory", "POST", "Export the sandbox workspace to a target host directory.", "write", true, false, new Dictionary<string, string> { ["target_directory"] = "Empty host directory for sandbox export." }),
            new("select_lab_agent", "/nexuscentral/select_lab_agent", "POST", "Select the active Lab agent.", "write", true, false, new Dictionary<string, string> { ["agent_id"] = "Agent wrapper id." }),
            new("select_lab_model", "/nexuscentral/select_lab_model", "POST", "Select the active Lab model.", "write", true, false, new Dictionary<string, string> { ["model_id"] = "Model id or instance id." }),
            new("set_lab_agent_setting", "/nexuscentral/set_lab_agent_setting", "POST", "Set an exposed Lab agent setting by id.", "write", true, false, new Dictionary<string, string> { ["setting_id"] = "Exposed setting id.", ["value"] = "New setting value." }),
            new("type_text", "/nexuscentral/type_text", "POST", "Type text semantically into the chat input, appending to the existing value.", "write", true, false, new Dictionary<string, string> { ["element_id"] = "Semantic input element id.", ["text"] = "Text to append into the input." }),
            new("set_text", "/nexuscentral/set_text", "POST", "Set the chat input text semantically.", "write", true, false, new Dictionary<string, string> { ["element_id"] = "Semantic input element id.", ["text"] = "Text to place into the input." }),
            new("set_chat_tokens", "/nexuscentral/set_chat_tokens", "POST", "Set the chat token slider value semantically.", "write", true, false, new Dictionary<string, string> { ["value"] = "Token count between 100 and 5000." }),
            new("click_button", "/nexuscentral/click_button", "POST", "Activate a semantic chat button such as send or reload.", "write", true, false, new Dictionary<string, string> { ["element_id"] = "Semantic button id." }),
            new("chat_send", "/nexuscentral/chat_send", "POST", "Submit a chat prompt through the live chat view model and return a run id.", "write", true, false, new Dictionary<string, string> { ["text"] = "Prompt text to send.", ["model_id"] = "Optional catalog model id." }),
            new("get_run_status", "/nexuscentral/get_run_status", "GET", "Inspect the status of an in-flight or completed chat run.", "read", true, false, new Dictionary<string, string> { ["run_id"] = "Chat run id." }),
        };

        _integrations = new List<IntegrationDefinition>
        {
            new("nexuscentral", "NexusCentral", "Core operational tools for the local desktop runtime.", true, "OK", nexuscentralTools),
            new("trello", "Trello", "Planned external integration.", false, "PLANNED", new List<ToolDefinition>()),
            new("slack", "Slack", "Planned external integration.", false, "PLANNED", new List<ToolDefinition>()),
            new("browser", "Browser", "Planned external integration.", false, "PLANNED", new List<ToolDefinition>()),
        };
    }

    public IReadOnlyList<IntegrationDefinition> GetIntegrations() => _integrations;

    public IntegrationDefinition? GetIntegration(string integrationId) =>
        _integrations.FirstOrDefault(x => x.Id == integrationId);

    public IReadOnlyList<IntegrationDefinition> GetEnabledIntegrations() =>
        _integrations.Where(integration => integration.Enabled).ToList();

    public IReadOnlyList<ToolDefinition> GetTools(string integrationId) =>
        GetIntegration(integrationId)?.Tools ?? new List<ToolDefinition>();

    public ToolDefinition? FindTool(string integrationId, string toolName) =>
        GetTools(integrationId).FirstOrDefault(tool => tool.Enabled && string.Equals(tool.Name, toolName, System.StringComparison.OrdinalIgnoreCase));
}
