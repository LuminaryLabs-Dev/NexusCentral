using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using NexusCentral_Avalonia.Models;
using NexusCentral_Avalonia.ViewModels;
using NexusCentral_Avalonia.Views;

namespace NexusCentral_Avalonia.Services;

public class IntegrationServer
{
    private const string LabTabName = "Lab";
    private readonly AppServices _services;
    private static readonly JsonSerializerOptions WorkflowJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
    };
    private readonly Dictionary<string, HostedInstanceListener> _hostedInstanceListeners = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _hostedListenerGate = new();
    private readonly List<object> _humanViewActions = new();
    private readonly List<object> _humanViewConsole = new();
    private readonly object _humanViewGate = new();
    private string _humanViewProjectPath = string.Empty;
    private string _humanViewPreviewUrl = string.Empty;
    private int _humanViewCursorX;
    private int _humanViewCursorY;
    private HttpListener? _listener;
    private bool _isRunning;
    private int _controlPlanePort = 3005;
    private string _advertisedHost = "127.0.0.1";

    public IntegrationServer(AppServices services)
    {
        _services = services;
        _services.Library.LibraryInventoryChanged += OnLibraryInventoryChanged;
        _services.Library.InstanceChanged += _ => OnLibraryInventoryChanged();
        _services.LanRouting.Changed += OnLanRoutingSettingsChanged;
    }

    public string LoopbackBaseUrl => $"http://127.0.0.1:{_controlPlanePort}";
    public string AdvertisedBaseUrl => $"http://{_advertisedHost}:{_controlPlanePort}";
    public string AdvertisedHost => _advertisedHost;

    public void Start(int port = 3005)
    {
        _controlPlanePort = port;
        var lanSettings = _services.LanRouting.GetSnapshot();
        _advertisedHost = lanSettings.AdvertisedHost;
        _listener = new HttpListener();
        var prefixes = new List<string>
        {
            $"http://localhost:{port}/",
            $"http://127.0.0.1:{port}/"
        };
        if (lanSettings.ExposeControlPlaneOnLan &&
            !string.Equals(_advertisedHost, "127.0.0.1", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(_advertisedHost, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            prefixes.Add($"http://{_advertisedHost}:{port}/");
        }

        foreach (var prefix in prefixes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                _listener.Prefixes.Add(prefix);
            }
            catch
            {
            }
        }
        _listener.Start();
        _isRunning = true;

        Task.Run(RunLoop);
        SyncHostedInstanceListeners();
        var lanStatus = lanSettings.ExposeControlPlaneOnLan
            ? $"LAN control plane active on {AdvertisedBaseUrl}."
            : "LAN control plane disabled.";
        _services.Logs.Append("info", "integration", $"Integration server listening on {LoopbackBaseUrl}. {lanStatus}");
    }

    public void Stop()
    {
        _isRunning = false;
        StopHostedInstanceListeners();
        _listener?.Stop();
        _listener?.Close();
    }

    private void OnLibraryInventoryChanged()
    {
        if (!_isRunning)
        {
            return;
        }

        Task.Run(SyncHostedInstanceListeners);
    }

    private void OnLanRoutingSettingsChanged()
    {
        if (!_isRunning)
        {
            return;
        }

        Task.Run(() =>
        {
            try
            {
                Stop();
                Start(_controlPlanePort);
            }
            catch (Exception ex)
            {
                _services.Logs.Append("error", "integration", $"Could not restart the LAN control plane. {ex.Message}");
            }
        });
    }

    private void SyncHostedInstanceListeners()
    {
        if (!_isRunning)
        {
            return;
        }

        var loadedInstances = _services.Library.GetLoadedInstances();
        var desiredHostedInstances = loadedInstances
            .Where(instance =>
                instance.AllowLanAccess &&
                instance.IsActive &&
                string.Equals(instance.LoadState, "resident", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(instance => instance.InstanceId, StringComparer.OrdinalIgnoreCase);

        lock (_hostedListenerGate)
        {
            foreach (var existing in _hostedInstanceListeners.ToList())
            {
                if (!desiredHostedInstances.TryGetValue(existing.Key, out var snapshot) || snapshot.HostedPort != existing.Value.Port)
                {
                    existing.Value.Stop();
                    _hostedInstanceListeners.Remove(existing.Key);
                }
            }

            foreach (var instance in loadedInstances)
            {
                if (!instance.AllowLanAccess)
                {
                    _services.Library.UpdateHostedInstanceStatus(instance.InstanceId, "disabled", "LAN exposure off");
                }
                else if (!instance.IsActive || !string.Equals(instance.LoadState, "resident", StringComparison.OrdinalIgnoreCase))
                {
                    _services.Library.UpdateHostedInstanceStatus(instance.InstanceId, "waiting", $"Waiting for a resident active instance before hosting on port {instance.HostedPort}");
                }
            }

            foreach (var snapshot in desiredHostedInstances.Values)
            {
                if (_hostedInstanceListeners.ContainsKey(snapshot.InstanceId))
                {
                    _services.Library.UpdateHostedInstanceStatus(snapshot.InstanceId, "listening", BuildHostedStatusMessage(snapshot.HostedPort));
                    continue;
                }

                try
                {
                    var listener = new HostedInstanceListener(_services, snapshot.InstanceId, snapshot.HostedPort, _advertisedHost);
                    listener.Start();
                    _hostedInstanceListeners[snapshot.InstanceId] = listener;
                    _services.Library.UpdateHostedInstanceStatus(snapshot.InstanceId, "listening", BuildHostedStatusMessage(snapshot.HostedPort));
                    _services.Logs.Append("info", "integration", $"Hosted runtime listener started for {snapshot.InstanceId} on port {snapshot.HostedPort}.", snapshot.InstanceId);
                }
                catch (Exception ex)
                {
                    _services.Library.UpdateHostedInstanceStatus(snapshot.InstanceId, "error", $"Failed to host runtime on port {snapshot.HostedPort}. {ex.Message}");
                    _services.Logs.Append("error", "integration", $"Failed to host runtime listener for {snapshot.InstanceId} on port {snapshot.HostedPort}. {ex.Message}", snapshot.InstanceId);
                }
            }
        }
    }

    private void StopHostedInstanceListeners()
    {
        lock (_hostedListenerGate)
        {
            foreach (var listener in _hostedInstanceListeners.Values)
            {
                listener.Stop();
            }

            _hostedInstanceListeners.Clear();
        }
    }

    private string BuildHostedStatusMessage(int port)
    {
        return string.Equals(_advertisedHost, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            ? $"Hosted on http://127.0.0.1:{port}"
            : $"Hosted on http://{_advertisedHost}:{port} | Loopback http://127.0.0.1:{port}";
    }

    private static string ResolveAdvertisedHost()
    {
        try
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up &&
                    adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var adapter in networkInterfaces)
            {
                var address = adapter.GetIPProperties()
                    .UnicastAddresses
                    .Select(item => item.Address)
                    .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork && !ip.ToString().StartsWith("169.254.", StringComparison.Ordinal));

                if (address is not null)
                {
                    return address.ToString();
                }
            }
        }
        catch
        {
        }

        return "127.0.0.1";
    }

    private static bool IsChatTabName(string? tabName) =>
        string.Equals(tabName, LabTabName, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(tabName, "Chat", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(tabName, "Chat (LLM)", StringComparison.OrdinalIgnoreCase);

    private async Task RunLoop()
    {
        while (_isRunning)
        {
            try
            {
                var context = await _listener!.GetContextAsync();
                _ = ProcessRequestAsync(context);
            }
            catch (HttpListenerException)
            {
            }
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        response.ContentType = "application/json";
        ApplyCorsHeaders(request, response);

        if (request.HttpMethod == "OPTIONS")
        {
            response.StatusCode = 200;
            response.Close();
            return;
        }

        object result;
        var path = request.Url?.AbsolutePath ?? string.Empty;

        try
        {
            _services.Logs.Append("info", "integration", $"{request.HttpMethod} {path}");
            if (!IsAuthorizedRequest(request))
            {
                response.StatusCode = 401;
                result = CreateErrorResponse(GetToolName(path), "UNAUTHORIZED", "Missing or invalid LAN routing token.");
            }
            else if (string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(path, "/nexuscentral/thinking-stream", StringComparison.OrdinalIgnoreCase))
            {
                await HandleThinkingStreamAsync(request, response);
                return;
            }
            else
            {
                result = (request.HttpMethod, path) switch
                {
                    ("GET", "/nexuscentral/list_tools") => HandleListTools(),
                    ("GET", "/nexuscentral/status") => HandleStatus(),
                    ("GET", "/nexuscentral/get_tabs") => HandleGetTabs(),
                    ("GET", "/nexuscentral/get_current_tab") => HandleGetCurrentTab(),
                    ("POST", "/nexuscentral/change_tab") => await HandleChangeTabAsync(request),
                    ("GET", "/nexuscentral/get_visible_elements") => await HandleGetVisibleElementsAsync(),
                    ("GET", "/nexuscentral/get_form_state") => await HandleGetFormStateAsync(),
                    ("POST", "/nexuscentral/click_element") => await HandleClickElementAsync(request),
                    ("POST", "/nexuscentral/select_list_item") => await HandleSelectListItemAsync(request),
                    ("GET", "/nexuscentral/screenshot") => await HandleScreenshotAsync("screenshot"),
                    ("GET", "/nexuscentral/get_screenshot") => await HandleScreenshotAsync("get_screenshot"),
                    ("GET", "/nexuscentral/list_templates") => HandleListTemplates(),
                    ("GET", "/nexuscentral/get_template") => HandleGetTemplate(request),
                    ("GET", "/nexuscentral/list_workflows") => HandleListWorkflows(),
                    ("GET", "/nexuscentral/get_workflow") => HandleGetWorkflow(request),
                    ("POST", "/nexuscentral/save_workflow") => await HandleSaveWorkflowAsync(request),
                    ("POST", "/nexuscentral/load_workflow_bundle") => await HandleLoadWorkflowBundleAsync(request),
                    ("POST", "/nexuscentral/save_workflow_bundle") => await HandleSaveWorkflowBundleAsync(request),
                    ("POST", "/nexuscentral/start_workflow_run") => await HandleStartWorkflowRunAsync(request),
                    ("POST", "/nexuscentral/start_workflow_step") => await HandleStartWorkflowStepAsync(request),
                    ("POST", "/nexuscentral/stop_workflow_run") => await HandleStopWorkflowRunAsync(request),
                    ("GET", "/nexuscentral/get_workflow_run") => HandleGetWorkflowRun(request),
                    ("GET", "/nexuscentral/get_workflow_step_run") => HandleGetWorkflowStepRun(request),
                    ("GET", "/nexuscentral/list_wrappers") => HandleListWrappers(),
                    ("GET", "/nexuscentral/get_wrapper") => HandleGetWrapper(request),
                    ("POST", "/nexuscentral/import_wrapper") => await HandleImportWrapperAsync(request),
                    ("POST", "/nexuscentral/validate_wrapper") => await HandleValidateWrapperAsync(request),
                    ("POST", "/nexuscentral/run_wrapper") => await HandleRunWrapperAsync(request),
                    ("GET", "/nexuscentral/get_wrapper_run") => HandleGetWrapperRun(request),
                    ("POST", "/nexuscentral/cancel_wrapper_run") => await HandleCancelWrapperRunAsync(request),
                    ("GET", "/nexuscentral/get_wrapper_run_artifacts") => HandleGetWrapperRunArtifacts(request),
                    ("POST", "/nexuscentral/apply_template_to_chat") => await HandleApplyTemplateToChatAsync(request),
                    ("GET", "/nexuscentral/list_models") => HandleListModels(),
                    ("GET", "/nexuscentral/get_model_catalog") => HandleGetModelCatalog(),
                    ("GET", "/nexuscentral/get_runtime_inventory") => HandleGetRuntimeInventory(),
                    ("GET", "/nexuscentral/get_runtime_roles") => HandleGetRuntimeRoles(),
                    ("GET", "/nexuscentral/get_model_health") => HandleGetModelHealth(request),
                    ("POST", "/nexuscentral/import_local_model") => await HandleImportLocalModelAsync(request),
                    ("POST", "/nexuscentral/verify_model") => await HandleVerifyModelAsync(request),
                    ("POST", "/nexuscentral/repair_model") => await HandleRepairModelAsync(request),
                    ("POST", "/nexuscentral/quarantine_model") => await HandleQuarantineModelAsync(request),
                    ("POST", "/nexuscentral/preload_model") => await HandlePreloadModelAsync(request),
                    ("POST", "/nexuscentral/pin_model") => await HandlePinModelAsync(request),
                    ("POST", "/nexuscentral/unpin_model") => await HandleUnpinModelAsync(request),
                    ("POST", "/nexuscentral/activate_local_model") => await HandleActivateLocalModelAsync(request),
                    ("POST", "/nexuscentral/activate_instance") => await HandleActivateInstanceAsync(request),
                    ("POST", "/nexuscentral/deactivate_local_model") => await HandleDeactivateLocalModelAsync(request),
                    ("POST", "/nexuscentral/reload_model") => await HandleReloadModelAsync(request),
                    ("POST", "/nexuscentral/delete_instance") => await HandleDeleteInstanceAsync(request),
                    ("POST", "/nexuscentral/set_instance_mode") => await HandleSetInstanceModeAsync(request),
                    ("POST", "/nexuscentral/set_instance_tokens") => await HandleSetInstanceTokensAsync(request),
                    ("POST", "/nexuscentral/set_instance_memory_capacity") => await HandleSetInstanceMemoryCapacityAsync(request),
                    ("GET", "/nexuscentral/get_instance_console") => HandleGetInstanceConsole(request),
                    ("POST", "/nexuscentral/set_runtime_roles") => await HandleSetRuntimeRolesAsync(request),
                    ("POST", "/nexuscentral/pull_model") => await HandlePullModelAsync(request),
                    ("GET", "/nexuscentral/get_logs") => HandleGetLogs(request),
                    ("POST", "/nexuscentral/instruct") => await HandleInstructAsync(request),
                    ("POST", "/nexuscentral/thinking-raw") => await HandleThinkingRawAsync(request),
                    ("POST", "/nexuscentral/thinking") => await HandleThinkingAsync(request),
                    ("GET", "/nexuscentral/get_chat_state") => await HandleGetChatStateAsync(),
                    ("GET", "/nexuscentral/get_lab_bootstrap") => await HandleGetLabBootstrapAsync(),
                    ("GET", "/nexuscentral/get_lab_state") => await HandleGetLabStateAsync(),
                    ("GET", "/nexuscentral/list_workspaces") => await HandleListWorkspacesAsync(),
                    ("POST", "/nexuscentral/select_workspace") => await HandleSelectWorkspaceAsync(request),
                    ("POST", "/nexuscentral/open_workspace") => await HandleOpenWorkspaceAsync(request),
                    ("POST", "/nexuscentral/create_sandbox") => await HandleCreateSandboxAsync(request),
                    ("POST", "/nexuscentral/delete_sandbox") => await HandleDeleteSandboxAsync(request),
                    ("GET", "/nexuscentral/get_workspace_tree") => HandleGetWorkspaceTree(request),
                    ("GET", "/nexuscentral/get_workspace_file") => HandleGetWorkspaceFile(request),
                    ("POST", "/nexuscentral/select_workspace_node") => await HandleSelectWorkspaceNodeAsync(request),
                    ("POST", "/nexuscentral/save_sandbox_file") => await HandleSaveSandboxFileAsync(request),
                    ("POST", "/nexuscentral/delete_sandbox_path") => await HandleDeleteSandboxPathAsync(request),
                    ("POST", "/nexuscentral/clear_sandbox") => await HandleClearSandboxAsync(request),
                    ("POST", "/nexuscentral/create_sandbox_directory") => await HandleCreateSandboxDirectoryAsync(request),
                    ("POST", "/nexuscentral/rename_sandbox_path") => await HandleRenameSandboxPathAsync(request),
                    ("POST", "/nexuscentral/save_sandbox_to_directory") => await HandleSaveSandboxToDirectoryAsync(request),
                    ("GET", "/nexuscentral/get_lab_suite_projects") => HandleGetLabSuiteProjects(request),
                    ("POST", "/nexuscentral/run_lab_suite_project") => await HandleRunLabSuiteProjectAsync(request),
                    ("POST", "/nexuscentral/open_human_view_preview") => await HandleOpenHumanViewPreviewAsync(request),
                    ("POST", "/nexuscentral/human_view_action") => await HandleHumanViewActionAsync(request),
                    ("GET", "/nexuscentral/human_view_snapshot") => HandleHumanViewSnapshot(),
                    ("GET", "/nexuscentral/human_view_console") => HandleHumanViewConsole(),
                    ("POST", "/nexuscentral/human_view_cursor") => await HandleHumanViewCursorAsync(request),
                    ("POST", "/nexuscentral/select_lab_agent") => await HandleSelectLabAgentAsync(request),
                    ("POST", "/nexuscentral/select_lab_model") => await HandleSelectLabModelAsync(request),
                    ("POST", "/nexuscentral/set_lab_agent_setting") => await HandleSetLabAgentSettingAsync(request),
                    ("POST", "/nexuscentral/type_text") => await HandleTypeTextAsync(request),
                    ("POST", "/nexuscentral/set_text") => await HandleSetTextAsync(request),
                    ("POST", "/nexuscentral/set_chat_tokens") => await HandleSetChatTokensAsync(request),
                    ("POST", "/nexuscentral/click_button") => await HandleClickButtonAsync(request),
                    ("POST", "/nexuscentral/chat_send") => await HandleChatSendAsync(request),
                    ("GET", "/nexuscentral/get_run_status") => await HandleGetRunStatusAsync(request),
                    _ => CreateNotFoundResponse(path),
                };
            }
        }
        catch (FileNotFoundException ex)
        {
            response.StatusCode = 404;
            result = CreateErrorResponse(GetToolName(path), "FILE_NOT_FOUND", ex.Message);
        }
        catch (Exception ex)
        {
            response.StatusCode = 500;
            _services.Logs.Append("error", "integration", ex.Message);
            result = CreateErrorResponse(GetToolName(path), "INTERNAL_ERROR", ex.Message);
        }

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(result);
        response.ContentLength64 = jsonBytes.Length;

        try
        {
            await response.OutputStream.WriteAsync(jsonBytes);
        }
        catch
        {
        }
        finally
        {
            response.Close();
        }
    }

    private object HandleListTools()
    {
        var integration = _services.Integrations.GetIntegration("nexuscentral");
        if (integration is null)
        {
            return CreateErrorResponse("list_tools", "INTEGRATION_NOT_FOUND", "The nexuscentral integration is not registered.");
        }

        return CreateSuccessResponse("list_tools", new
        {
            integration = new
            {
                integration_id = integration.Id,
                display_name = integration.Name,
                description = integration.Description,
                enabled = integration.Enabled,
                status = integration.Status,
            },
            tools = integration.Tools.Select(tool => new
            {
                name = tool.Name,
                route = tool.Route,
                method = tool.Method,
                description = tool.Description,
                auth_scope = tool.AuthScope,
                enabled = tool.Enabled,
                dangerous = tool.Dangerous,
                input_contract = tool.InputContract,
            })
        });
    }

    private object HandleStatus()
    {
        var lanSettings = _services.LanRouting.GetSnapshot();
        return CreateSuccessResponse("status", new
        {
            status_text = _services.AppState.StatusText,
            summary_text = _services.AppState.SummaryText,
            display_name = lanSettings.DisplayName,
            local_peer_id = lanSettings.LocalPeerId,
            current_tab = _services.AppState.CurrentTab,
            tabs = _services.AppState.Tabs,
            model_count = _services.Catalog.GetEntries().Count,
            advertised_host = lanSettings.AdvertisedHost,
            control_plane_port = lanSettings.ControlPlanePort,
            lan_control_plane_enabled = lanSettings.ExposeControlPlaneOnLan,
            lan_auth_header = LanRoutingSettingsService.AuthHeaderName,
            base_directory = _services.Paths.BaseDirectory,
            bundle_root_directory = _services.Paths.BundleRootDirectory,
            resolved_root_directory = _services.Paths.ResolvedRootDirectory,
            state_root_directory = _services.Paths.StateRootDirectory,
            runtime_directory = _services.Paths.RuntimeDirectory,
            models_directory = _services.Paths.ModelsDirectory,
            logs_directory = _services.Paths.LogsDirectory,
            bundle_root_override_active = _services.Paths.BundleRootOverrideActive,
            bundle_root_override_path = _services.Paths.BundleRootOverridePath,
            state_root_override_active = _services.Paths.StateRootOverrideActive,
            state_root_override_path = _services.Paths.StateRootOverridePath,
            path_mode = _services.Paths.PathMode,
            state_path_mode = _services.Paths.StatePathMode,
            timestamp_utc = DateTimeOffset.UtcNow,
        });
    }

    private object HandleGetTabs()
    {
        return CreateSuccessResponse("get_tabs", new
        {
            tabs = _services.AppState.Tabs,
            count = _services.AppState.Tabs.Count,
        });
    }

    private object HandleGetCurrentTab()
    {
        return CreateSuccessResponse("get_current_tab", new
        {
            current_tab = _services.AppState.CurrentTab
        });
    }

    private async Task<object> HandleChangeTabAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        if (!body.TryGetProperty("tab", out var tabElement))
        {
            return CreateErrorResponse("change_tab", "INVALID_INPUT", "Missing 'tab' parameter.");
        }

        var tab = tabElement.GetString();
        if (string.IsNullOrWhiteSpace(tab))
        {
            return CreateErrorResponse("change_tab", "INVALID_INPUT", "Tab parameter is empty.");
        }

        var success = await ChangeTabAsync(tab);
        return success
            ? CreateSuccessResponse("change_tab", new { tab = _services.AppState.CurrentTab })
            : CreateErrorResponse("change_tab", "TAB_NOT_FOUND", $"Tab '{tab}' was not found.");
    }

    private async Task<object> HandleScreenshotAsync(string toolName)
    {
        var capture = await _services.Screenshots.CaptureAsync();
        return capture is null
            ? CreateErrorResponse(toolName, "SCREENSHOT_FAILED", "Could not capture the desktop window.")
            : CreateSuccessResponse(toolName, new
            {
                image_base64 = capture.ImageBase64,
                image = capture.ImageBase64,
                format = capture.Format,
                width = capture.Width,
                height = capture.Height,
                captured_at = capture.CapturedAt,
                active_tab = string.IsNullOrWhiteSpace(capture.ActiveTab) ? _services.AppState.CurrentTab ?? string.Empty : capture.ActiveTab,
                current_view_summary = BuildCurrentViewSummary()
            });
    }

    private string BuildCurrentViewSummary()
    {
        var currentTab = _services.AppState.CurrentTab ?? string.Empty;

        if (IsChatTabName(currentTab))
        {
            var chat = _services.Chat;
            if (chat is null)
            {
                return "Lab view not ready.";
            }

            var selectedModel = chat.SelectedModel?.DisplayName ?? "Automatic";
            var workspace = chat.SelectedWorkspace?.Name ?? "No workspace";
            return $"{chat.Messages.Count} messages | {chat.GenerationStatusText} | {selectedModel} | {workspace}";
        }

        if (string.Equals(currentTab, "Models", StringComparison.OrdinalIgnoreCase))
        {
            var loadedCount = _services.Library.GetLoadedInstances().Count;
            var catalogCount = _services.Catalog.GetEntries().Count;
            return $"{loadedCount} loaded | {catalogCount} catalog";
        }

        if (string.Equals(currentTab, "Router", StringComparison.OrdinalIgnoreCase))
        {
            var peers = _services.PeerRegistry.GetPeers().Count;
            var routingTargets = _services.Library.GetRoutingInstances();
            var localCount = routingTargets.Count(target => !target.IsRemote);
            var importedCount = routingTargets.Count(target => target.IsRemote);
            return $"{peers} peers | {importedCount} imported | {localCount} local";
        }

        if (string.Equals(currentTab, "Integrations", StringComparison.OrdinalIgnoreCase))
        {
            var integrations = _services.Integrations.GetEnabledIntegrations().Count;
            var tools = _services.Integrations.GetTools("nexuscentral").Count;
            return $"{integrations} integrations | {tools} tools";
        }

        if (string.Equals(currentTab, "Agents", StringComparison.OrdinalIgnoreCase))
        {
            var chat = _services.Chat;
            var selectedAgent = chat?.SelectedAgent?.Name ?? "No agent selected";
            return selectedAgent;
        }

        if (string.Equals(currentTab, "Workflows", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(currentTab, "Templates", StringComparison.OrdinalIgnoreCase))
        {
            var workflows = _services.WorkflowViewModel;
            if (workflows?.CurrentWorkflow is null)
            {
                return "Workflow editor idle.";
            }

            return $"{workflows.CurrentWorkflow.Name} | {workflows.CurrentWorkflow.Steps.Count} steps";
        }

        return string.IsNullOrWhiteSpace(_services.AppState.SummaryText)
            ? "View summary unavailable."
            : _services.AppState.SummaryText;
    }

    private object HandleListTemplates()
    {
        var templates = _services.Templates.GetTemplates().Select(template => new
        {
            id = template.Id,
            name = template.Name,
            description = template.Description,
            default_mode = template.DefaultMode,
            default_wrapper_id = template.DefaultWrapperId,
            allowed_wrapper_ids = template.AllowedWrapperIds,
            wrapper_locked = template.WrapperLocked,
            input_summary = template.InputSummary,
            artifact_outputs = template.ArtifactOutputs.Globs,
            default_model_id = template.DefaultModelId,
            default_max_tokens = template.DefaultMaxTokens,
            tags = template.Tags,
            source = template.Source,
            valid = template.IsValid,
            validation_message = template.ValidationMessage
        });

        return CreateSuccessResponse("list_templates", new { templates });
    }

    private object HandleGetTemplate(HttpListenerRequest request)
    {
        var templateId = request.QueryString["template_id"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return CreateErrorResponse("get_template", "INVALID_INPUT", "Missing 'template_id' query parameter.");
        }

        var template = _services.Templates.FindById(templateId);
        return template is null
            ? CreateErrorResponse("get_template", "TEMPLATE_NOT_FOUND", $"Template '{templateId}' was not found.")
            : CreateSuccessResponse("get_template", new
            {
                id = template.Id,
                name = template.Name,
                description = template.Description,
                default_mode = template.DefaultMode,
                default_wrapper_id = template.DefaultWrapperId,
                allowed_wrapper_ids = template.AllowedWrapperIds,
                wrapper_locked = template.WrapperLocked,
                inputs = template.Inputs.Select(input => new
                {
                    id = input.Id,
                    label = input.Label,
                    kind = input.Kind,
                    required = input.Required,
                    maps_to = input.MapsTo
                }),
                artifact_outputs = new
                {
                    discover = template.ArtifactOutputs.Discover,
                    globs = template.ArtifactOutputs.Globs
                },
                default_model_id = template.DefaultModelId,
                default_max_tokens = template.DefaultMaxTokens,
                default_args = template.DefaultArgs,
                tags = template.Tags,
                source = template.Source,
                source_path = template.SourcePath,
                valid = template.IsValid,
                validation_message = template.ValidationMessage
            });
    }

    private object HandleListWorkflows()
    {
        var workflows = _services.Workflows.GetWorkflows().Select(workflow => new
        {
            id = workflow.Id,
            name = workflow.Name,
            description = workflow.Description,
            working_directory = workflow.WorkingDirectory,
            step_count = workflow.Steps.Count,
            input_count = workflow.Inputs.Count,
            default_wrapper_id = workflow.GlobalSettings.DefaultWrapperId,
            default_model_id = workflow.GlobalSettings.DefaultModelId,
            stop_on_error = workflow.GlobalSettings.StopOnError,
            tags = workflow.Tags,
            source = workflow.Source,
            source_path = workflow.SourcePath
        });

        return CreateSuccessResponse("list_workflows", new { workflows });
    }

    private object HandleGetWorkflow(HttpListenerRequest request)
    {
        var workflowId = request.QueryString["workflow_id"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(workflowId))
        {
            return CreateErrorResponse("get_workflow", "INVALID_INPUT", "Missing 'workflow_id' query parameter.");
        }

        var workflow = _services.Workflows.FindById(workflowId);
        return workflow is null
            ? CreateErrorResponse("get_workflow", "WORKFLOW_NOT_FOUND", $"Workflow '{workflowId}' was not found.")
            : CreateSuccessResponse("get_workflow", BuildWorkflowPayload(workflow));
    }

    private async Task<object> HandleSaveWorkflowAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var workflow = ResolveWorkflowFromBody(body);
        if (workflow is null)
        {
            return CreateErrorResponse("save_workflow", "INVALID_INPUT", "Provide either 'workflow_id' or an inline 'workflow' object.");
        }

        var path = _services.Workflows.SaveWorkflow(workflow);
        var saved = _services.Workflows.FindById(workflow.Id) ?? workflow;
        return CreateSuccessResponse("save_workflow", new
        {
            path,
            workflow = BuildWorkflowPayload(saved)
        });
    }

    private async Task<object> HandleLoadWorkflowBundleAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var bundleId = body.TryGetProperty("bundle_id", out var bundleIdElement) ? bundleIdElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(bundleId))
        {
            return CreateErrorResponse("load_workflow_bundle", "INVALID_INPUT", "Missing 'bundle_id' parameter.");
        }

        var bundle = _services.Workflows.LoadBundle(bundleId);
        return bundle is null
            ? CreateErrorResponse("load_workflow_bundle", "BUNDLE_NOT_FOUND", $"Workflow bundle '{bundleId}' was not found.")
            : CreateSuccessResponse("load_workflow_bundle", BuildWorkflowBundlePayload(bundle));
    }

    private async Task<object> HandleSaveWorkflowBundleAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var bundleId = body.TryGetProperty("bundle_id", out var bundleIdElement) ? bundleIdElement.GetString() : null;
        var bundle = ResolveWorkflowBundleFromBody(body);
        if (bundle is null)
        {
            return CreateErrorResponse("save_workflow_bundle", "INVALID_INPUT", "Provide a 'bundle' object or a 'workflow' object.");
        }

        var path = _services.Workflows.SaveBundle(bundle, bundleId);
        var saved = _services.Workflows.LoadBundle(path) ?? bundle;
        return CreateSuccessResponse("save_workflow_bundle", new
        {
            path,
            bundle = BuildWorkflowBundlePayload(saved)
        });
    }

    private async Task<object> HandleStartWorkflowRunAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var workflow = ResolveWorkflowFromBody(body);
        if (workflow is null)
        {
            return CreateErrorResponse("start_workflow_run", "INVALID_INPUT", "Provide either 'workflow_id' or an inline 'workflow' object.");
        }

        var inputValues = body.TryGetProperty("input_values", out var inputValuesElement)
            ? ReadStringDictionary(inputValuesElement)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var handle = _services.WorkflowRuns.StartRun(workflow, inputValues);
        var run = _services.WorkflowRuns.GetRun(handle.RunId);
        return CreateSuccessResponse("start_workflow_run", new
        {
            run_id = handle.RunId,
            run = run is null ? null : BuildWorkflowRunPayload(run)
        });
    }

    private async Task<object> HandleStartWorkflowStepAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var workflow = ResolveWorkflowFromBody(body);
        var stepId = body.TryGetProperty("step_id", out var stepIdElement) ? stepIdElement.GetString() : null;
        if (workflow is null || string.IsNullOrWhiteSpace(stepId))
        {
            return CreateErrorResponse("start_workflow_step", "INVALID_INPUT", "Both workflow input and 'step_id' are required.");
        }

        var inputValues = body.TryGetProperty("input_values", out var inputValuesElement)
            ? ReadStringDictionary(inputValuesElement)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var handle = _services.WorkflowRuns.StartStepRun(workflow, stepId, inputValues);
        var run = _services.WorkflowRuns.GetRun(handle.RunId);
        return CreateSuccessResponse("start_workflow_step", new
        {
            run_id = handle.RunId,
            step_id = stepId,
            run = run is null ? null : BuildWorkflowRunPayload(run)
        });
    }

    private async Task<object> HandleStopWorkflowRunAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var runId = body.TryGetProperty("run_id", out var runIdElement) ? runIdElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(runId))
        {
            return CreateErrorResponse("stop_workflow_run", "INVALID_INPUT", "Missing 'run_id' parameter.");
        }

        var stopped = _services.WorkflowRuns.StopRun(runId);
        return stopped
            ? CreateSuccessResponse("stop_workflow_run", new { run_id = runId, stopped = true })
            : CreateErrorResponse("stop_workflow_run", "RUN_NOT_FOUND", $"Workflow run '{runId}' was not found.");
    }

    private object HandleGetWorkflowRun(HttpListenerRequest request)
    {
        var runId = request.QueryString["run_id"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(runId))
        {
            return CreateErrorResponse("get_workflow_run", "INVALID_INPUT", "Missing 'run_id' query parameter.");
        }

        var run = _services.WorkflowRuns.GetRun(runId);
        return run is null
            ? CreateErrorResponse("get_workflow_run", "RUN_NOT_FOUND", $"Workflow run '{runId}' was not found.")
            : CreateSuccessResponse("get_workflow_run", BuildWorkflowRunPayload(run));
    }

    private object HandleGetWorkflowStepRun(HttpListenerRequest request)
    {
        var runId = request.QueryString["run_id"] ?? string.Empty;
        var stepId = request.QueryString["step_id"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(runId) || string.IsNullOrWhiteSpace(stepId))
        {
            return CreateErrorResponse("get_workflow_step_run", "INVALID_INPUT", "Both 'run_id' and 'step_id' query parameters are required.");
        }

        var step = _services.WorkflowRuns.GetStepRun(runId, stepId);
        return step is null
            ? CreateErrorResponse("get_workflow_step_run", "STEP_NOT_FOUND", $"Step '{stepId}' was not found in run '{runId}'.")
            : CreateSuccessResponse("get_workflow_step_run", BuildWorkflowStepRunPayload(step));
    }

    private object HandleListWrappers()
    {
        var wrappers = _services.Wrappers.GetWrappers().Select(wrapper => new
        {
            id = wrapper.Id,
            name = wrapper.Name,
            description = wrapper.Description,
            transport = wrapper.Transport,
            io_contract_id = wrapper.IoContractId,
            text_input_roles = wrapper.TextInputRoles,
            text_output_roles = wrapper.TextOutputRoles,
            arg_templates = wrapper.ArgTemplates,
            settings = wrapper.Settings,
            supports_chat = wrapper.SupportsChat,
            supports_artifacts = wrapper.SupportsArtifacts,
            supports_templates = wrapper.SupportsTemplates,
            supports_hosted_web = wrapper.SupportsHostedWeb,
            supports_attachments = wrapper.SupportsAttachments,
            allowed_tools = wrapper.AllowedTools,
            web_chat_mode = wrapper.WebChatMode,
            default_timeout_sec = wrapper.DefaultTimeoutSec,
            input_contract = wrapper.InputContract,
            output_contract = wrapper.OutputContract,
            validation = wrapper.Validation,
            source_bundle_path = wrapper.SourceBundlePath,
            import_metadata = wrapper.ImportMetadata,
            source = wrapper.Source,
            available = wrapper.IsAvailable,
            availability_message = wrapper.AvailabilityMessage
        });

        return CreateSuccessResponse("list_wrappers", new { wrappers });
    }

    private object HandleGetWrapper(HttpListenerRequest request)
    {
        var wrapperId = request.QueryString["wrapper_id"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(wrapperId))
        {
            return CreateErrorResponse("get_wrapper", "INVALID_INPUT", "Missing 'wrapper_id' query parameter.");
        }

        var wrapper = _services.Wrappers.FindById(wrapperId);
        return wrapper is null
            ? CreateErrorResponse("get_wrapper", "WRAPPER_NOT_FOUND", $"Wrapper '{wrapperId}' was not found.")
            : CreateSuccessResponse("get_wrapper", new
            {
                id = wrapper.Id,
                name = wrapper.Name,
                description = wrapper.Description,
                transport = wrapper.Transport,
                command = wrapper.Command,
                args = wrapper.Args,
                io_contract_id = wrapper.IoContractId,
                text_input_roles = wrapper.TextInputRoles,
                text_output_roles = wrapper.TextOutputRoles,
                arg_templates = wrapper.ArgTemplates,
                settings = wrapper.Settings,
                env = wrapper.Env,
                supports_chat = wrapper.SupportsChat,
                supports_artifacts = wrapper.SupportsArtifacts,
                supports_templates = wrapper.SupportsTemplates,
                supports_hosted_web = wrapper.SupportsHostedWeb,
                supports_attachments = wrapper.SupportsAttachments,
                allowed_tools = wrapper.AllowedTools,
                web_chat_mode = wrapper.WebChatMode,
                default_timeout_sec = wrapper.DefaultTimeoutSec,
                input_contract = wrapper.InputContract,
                output_contract = wrapper.OutputContract,
                validation = wrapper.Validation,
                source_bundle_path = wrapper.SourceBundlePath,
                import_metadata = wrapper.ImportMetadata,
                source = wrapper.Source,
                source_path = wrapper.SourcePath,
                available = wrapper.IsAvailable,
                availability_message = wrapper.AvailabilityMessage
            });
    }

    private async Task<object> HandleImportWrapperAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var path = body.TryGetProperty("path", out var pathElement) ? pathElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(path))
        {
            return CreateErrorResponse("import_wrapper", "INVALID_INPUT", "Missing 'path' parameter.");
        }

        var result = await _services.Wrappers.ImportWrapperAsync(path);
        return result.Ok
            ? CreateSuccessResponse("import_wrapper", new
            {
                wrapper_id = result.WrapperId,
                persisted_path = result.PersistedPath,
                warnings = result.Warnings,
                wrapper = result.Wrapper is null ? null : new
                {
                    id = result.Wrapper.Id,
                    name = result.Wrapper.Name,
                    io_contract_id = result.Wrapper.IoContractId,
                    text_input_roles = result.Wrapper.TextInputRoles,
                    text_output_roles = result.Wrapper.TextOutputRoles,
                    arg_templates = result.Wrapper.ArgTemplates,
                    source_bundle_path = result.Wrapper.SourceBundlePath,
                    import_metadata = result.Wrapper.ImportMetadata,
                    supports_hosted_web = result.Wrapper.SupportsHostedWeb,
                    supports_attachments = result.Wrapper.SupportsAttachments,
                    allowed_tools = result.Wrapper.AllowedTools,
                    validation = result.Wrapper.Validation
                }
            })
            : CreateErrorResponse("import_wrapper", "IMPORT_FAILED", string.Join(" ", result.Errors.Concat(result.Warnings)));
    }

    private async Task<object> HandleValidateWrapperAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var wrapperId = body.TryGetProperty("wrapper_id", out var wrapperElement) ? wrapperElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(wrapperId))
        {
            return CreateErrorResponse("validate_wrapper", "INVALID_INPUT", "Missing 'wrapper_id' parameter.");
        }

        var result = await _services.Wrappers.ValidateWrapperAsync(wrapperId);
        return result.Ok
            ? CreateSuccessResponse("validate_wrapper", new
            {
                wrapper_id = result.WrapperId,
                resolved_command = result.ResolvedCommand,
                resolved_args = result.ResolvedArgs,
                tooling_reachable = result.ToolingReachable,
                warnings = result.Warnings
            })
            : CreateErrorResponse("validate_wrapper", "WRAPPER_INVALID", string.Join(" ", result.Errors.Concat(result.Warnings)));
    }

    private async Task<object> HandleRunWrapperAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var prompt = body.TryGetProperty("prompt", out var promptElement) ? promptElement.GetString() : null;
        var templateId = body.TryGetProperty("template_id", out var templateElement) ? templateElement.GetString() : null;
        var wrapperId = body.TryGetProperty("wrapper_id", out var wrapperElement) ? wrapperElement.GetString() : null;
        var modelId = body.TryGetProperty("model_id", out var modelElement) ? modelElement.GetString() : string.Empty;
        var sessionId = body.TryGetProperty("session_id", out var sessionElement) ? sessionElement.GetString() : string.Empty;
        var rootDirectory = body.TryGetProperty("root_directory", out var rootDirectoryElement) ? rootDirectoryElement.GetString() : string.Empty;
        var maxTokens = body.TryGetProperty("max_tokens", out var tokenElement) && tokenElement.TryGetInt32(out var parsedTokens) ? parsedTokens : 1200;
        var metadata = body.TryGetProperty("metadata", out var metadataElement) && metadataElement.ValueKind == JsonValueKind.Object
            ? metadataElement.EnumerateObject().ToDictionary(
                property => property.Name,
                property => property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => property.Value.ToString()
                },
                StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var artifactInputs = body.TryGetProperty("artifact_inputs", out var artifactInputsElement) && artifactInputsElement.ValueKind == JsonValueKind.Array
            ? artifactInputsElement.EnumerateArray().Select(item => item.GetString() ?? string.Empty).Where(item => !string.IsNullOrWhiteSpace(item)).ToList()
            : new List<string>();

        if (string.IsNullOrWhiteSpace(prompt) || string.IsNullOrWhiteSpace(templateId))
        {
            return CreateErrorResponse("run_wrapper", "INVALID_INPUT", "Missing 'prompt' or 'template_id' parameter.");
        }

        var handle = _services.WrapperRuns.StartRun(new WrapperRunRequest
        {
            TemplateId = templateId,
            WrapperId = wrapperId ?? string.Empty,
            Mode = "wrapper",
            Prompt = prompt,
            RootDirectory = rootDirectory ?? string.Empty,
            SessionId = sessionId ?? string.Empty,
            ArtifactInputs = artifactInputs,
            ModelId = modelId ?? string.Empty,
            MaxTokens = maxTokens,
            NexuscentralBaseUrl = "http://localhost:3005",
            LlmToolRoute = "/nexuscentral/thinking",
            Metadata = metadata
        });
        var snapshot = _services.WrapperRuns.GetRun(handle.RunId);
        return CreateSuccessResponse("run_wrapper", new
        {
            run_id = handle.RunId,
            template_id = snapshot?.TemplateId ?? templateId,
            wrapper_id = snapshot?.WrapperId ?? wrapperId,
            io_contract_id = snapshot?.IoContractId ?? string.Empty,
            session_id = snapshot?.SessionId ?? (sessionId ?? string.Empty),
            root_directory = snapshot?.RootDirectory ?? (rootDirectory ?? string.Empty),
            status = snapshot?.Status ?? "queued",
            output_dir = snapshot?.OutputDir ?? string.Empty
        });
    }

    private object HandleGetWrapperRun(HttpListenerRequest request)
    {
        var runId = request.QueryString["run_id"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(runId))
        {
            return CreateErrorResponse("get_wrapper_run", "INVALID_INPUT", "Missing 'run_id' query parameter.");
        }

        var state = _services.WrapperRuns.GetRun(runId);
        return state is null
            ? CreateErrorResponse("get_wrapper_run", "RUN_NOT_FOUND", $"Wrapper run '{runId}' was not found.")
            : CreateSuccessResponse("get_wrapper_run", BuildWrapperRunPayload(state));
    }

    private async Task<object> HandleCancelWrapperRunAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var runId = body.TryGetProperty("run_id", out var runElement) ? runElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(runId))
        {
            return CreateErrorResponse("cancel_wrapper_run", "INVALID_INPUT", "Missing 'run_id' parameter.");
        }

        var cancelled = _services.WrapperRuns.CancelRun(runId);
        return cancelled
            ? CreateSuccessResponse("cancel_wrapper_run", new { run_id = runId, cancelled = true })
            : CreateErrorResponse("cancel_wrapper_run", "CANCEL_FAILED", $"Wrapper run '{runId}' could not be cancelled.");
    }

    private object HandleGetWrapperRunArtifacts(HttpListenerRequest request)
    {
        var runId = request.QueryString["run_id"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(runId))
        {
            return CreateErrorResponse("get_wrapper_run_artifacts", "INVALID_INPUT", "Missing 'run_id' query parameter.");
        }

        var artifacts = _services.WrapperRuns.GetArtifacts(runId).Select(artifact => new
        {
            label = artifact.Label,
            path = artifact.Path,
            kind = artifact.Kind
        });
        return CreateSuccessResponse("get_wrapper_run_artifacts", new { run_id = runId, artifacts });
    }

    private async Task<object> HandleApplyTemplateToChatAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var templateId = body.TryGetProperty("template_id", out var templateElement) ? templateElement.GetString() : null;
        var wrapperId = body.TryGetProperty("wrapper_id", out var wrapperElement) ? wrapperElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return CreateErrorResponse("apply_template_to_chat", "INVALID_INPUT", "Missing 'template_id' parameter.");
        }

        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            if (chat is null)
            {
                return CreateErrorResponse("apply_template_to_chat", "CHAT_NOT_READY", "Chat view model is not available.");
            }

            if (!chat.ApplyTemplateById(templateId, wrapperId, out var error))
            {
                return CreateErrorResponse("apply_template_to_chat", "APPLY_FAILED", error);
            }

            var snapshot = chat.GetSnapshot();
            return CreateSuccessResponse("apply_template_to_chat", new
            {
                template_id = snapshot.TemplateId,
                template_name = snapshot.TemplateName,
                execution_mode = snapshot.ExecutionMode,
                wrapper_id = snapshot.WrapperId,
                wrapper_name = snapshot.WrapperName,
                supported_input_kinds = snapshot.SupportedInputKinds
            });
        });
    }

    private object HandleListModels()
    {
        var libraryItems = _services.Library.GetLibraryItems().ToDictionary(item => item.ModelId, StringComparer.OrdinalIgnoreCase);
        var items = _services.Catalog.GetEntries().Select(entry =>
        {
            libraryItems.TryGetValue(entry.ModelId, out var libraryItem);
            return new
            {
                model_id = entry.ModelId,
                display_name = entry.DisplayName,
                status = entry.Status,
                health_status = entry.HealthStatus,
                admission_status = entry.AdmissionStatus,
                runtime_backend = entry.RuntimeBackend,
                model_family = entry.ModelFamily,
                quant_type = entry.QuantType,
                estimated_memory_mb = entry.EstimatedMemoryMb,
                max_memory_mb = entry.MaxMemoryMb,
                max_threads = entry.MaxThreads,
                context_size = entry.ContextSize,
                is_runnable = entry.IsRunnable,
                load_state = libraryItem?.LoadState ?? "unloaded",
                is_loaded = libraryItem?.IsLoaded ?? false,
                is_pinned = libraryItem?.IsPinned ?? false,
                assigned_endpoints = libraryItem?.AssignedEndpoints ?? Array.Empty<string>(),
                instance_count = libraryItem?.InstanceCount ?? 0
            };
        });

        return CreateSuccessResponse("list_models", new { models = items });
    }

    private object HandleGetModelCatalog()
    {
        var libraryItems = _services.Library.GetLibraryItems().ToDictionary(item => item.ModelId, StringComparer.OrdinalIgnoreCase);
        var items = _services.Catalog.GetEntries().Select(entry =>
        {
            libraryItems.TryGetValue(entry.ModelId, out var libraryItem);
            return new
            {
                model_id = entry.ModelId,
                display_name = entry.DisplayName,
                local_path = entry.LocalPath,
                format = entry.Format,
                runtime_backend = entry.RuntimeBackend,
                is_runnable = entry.IsRunnable,
                runnable_reason = entry.RunnableReason,
                quantization = entry.Quantization,
                quant_type = entry.QuantType,
                model_family = entry.ModelFamily,
                estimated_memory_mb = entry.EstimatedMemoryMb,
                max_memory_mb = entry.MaxMemoryMb,
                max_threads = entry.MaxThreads,
                context_size = entry.ContextSize,
                max_loaded_instances = entry.MaxLoadedInstances,
                admission_status = entry.AdmissionStatus,
                source_repo = entry.SourceRepo,
                filename = entry.Filename,
                status = entry.Status,
                file_size_bytes = entry.FileSizeBytes,
                health_status = entry.HealthStatus,
                health_reason = entry.HealthReason,
                last_verified_utc = entry.LastVerifiedUtc,
                last_runtime_check_utc = entry.LastRuntimeCheckUtc,
                quarantine_path = entry.QuarantinePath,
                load_state = libraryItem?.LoadState ?? "unloaded",
                is_loaded = libraryItem?.IsLoaded ?? false,
                is_pinned = libraryItem?.IsPinned ?? false,
                assigned_endpoints = libraryItem?.AssignedEndpoints ?? Array.Empty<string>(),
                instance_count = libraryItem?.InstanceCount ?? 0
            };
        });

        return CreateSuccessResponse("get_model_catalog", new { models = items });
    }

    private object HandleGetRuntimeInventory()
    {
        var inventory = _services.Library.GetLoadedInstances().Select(item =>
        {
            var catalog = _services.Catalog.FindById(item.ModelId);
            return new
            {
                instance_id = item.InstanceId,
                model_id = item.ModelId,
                display_name = item.DisplayName,
                family = item.Family,
                load_state = item.LoadState,
                is_active = item.IsActive,
                is_pinned = item.IsPinned,
                primary_mode = item.PrimaryMode,
                capabilities = item.Capabilities,
                reserved_memory = item.MemoryReservedMb,
                actual_memory = item.MemoryActualMb,
                estimated_memory_mb = catalog?.EstimatedMemoryMb ?? 0,
                max_memory_mb = catalog?.MaxMemoryMb ?? 0,
                admission_status = catalog?.AdmissionStatus ?? string.Empty,
                max_tokens = item.MaxTokens,
                health_status = item.HealthStatus,
                allow_lan_access = item.AllowLanAccess,
                hosted_port = item.HostedPort,
                console_preview = item.ConsoleEntries.TakeLast(5).Select(entry => new
                {
                    timestamp_utc = entry.TimestampUtc,
                    source = entry.Source,
                    text = entry.Text
                })
            };
        });

        return CreateSuccessResponse("get_runtime_inventory", new { inventory });
    }

    private object HandleGetRuntimeRoles()
    {
        var roles = _services.Library.GetRoles().Select(role => new
        {
            role = role.RoleName,
            assigned_instance_id = role.AssignedInstanceId,
            assigned_model_id = role.AssignedModelId,
            assigned_model_display_name = role.AssignedModelDisplayName,
            routing_mode = role.RoutingMode,
            reserved_memory = role.ReservedMemory,
            max_parallel = role.MaxParallel,
            queue_limit = role.QueueLimit,
            live_status = role.LiveStatus
        });

        return CreateSuccessResponse("get_runtime_roles", new { roles });
    }

    private bool IsAuthorizedRequest(HttpListenerRequest request)
    {
        if (IsLoopbackRequest(request))
        {
            return true;
        }

        var lanSettings = _services.LanRouting.GetSnapshot();
        if (!lanSettings.ExposeControlPlaneOnLan)
        {
            return false;
        }

        var providedToken = request.Headers[LanRoutingSettingsService.AuthHeaderName];
        return LanRoutingSettingsService.SecureEquals(providedToken, lanSettings.SharedSecret);
    }

    private static bool IsLoopbackRequest(HttpListenerRequest request)
    {
        var address = request.RemoteEndPoint?.Address;
        return address is null || IPAddress.IsLoopback(address);
    }

    private void ApplyCorsHeaders(HttpListenerRequest request, HttpListenerResponse response)
    {
        var origin = request.Headers["Origin"];
        var allowedOrigin = ResolveAllowedCorsOrigin(origin);
        if (!string.IsNullOrWhiteSpace(allowedOrigin))
        {
            response.Headers.Add("Access-Control-Allow-Origin", allowedOrigin);
            response.Headers.Add("Vary", "Origin");
        }

        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", $"Content-Type, {LanRoutingSettingsService.AuthHeaderName}");
    }

    private string ResolveAllowedCorsOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        if (string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)))
        {
            return origin;
        }

        var lanSettings = _services.LanRouting.GetSnapshot();
        var webLabAdvertised = _services.WebLab.AdvertisedHost;
        if (lanSettings.ExposeControlPlaneOnLan &&
            string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(uri.Host, lanSettings.AdvertisedHost, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Host, webLabAdvertised, StringComparison.OrdinalIgnoreCase)))
        {
            return origin;
        }

        return string.Empty;
    }

    private object HandleGetModelHealth(HttpListenerRequest request)
    {
        var modelId = request.QueryString["model_id"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return CreateErrorResponse("get_model_health", "INVALID_INPUT", "Missing 'model_id' query parameter.");
        }

        var entry = _services.Catalog.GetModelHealth(modelId);
        if (entry is null)
        {
            return CreateErrorResponse("get_model_health", "MODEL_NOT_FOUND", $"Model '{modelId}' was not found.");
        }

        return CreateSuccessResponse("get_model_health", new
        {
            model_id = entry.ModelId,
            display_name = entry.DisplayName,
            status = entry.Status,
            health_status = entry.HealthStatus,
            health_reason = entry.HealthReason,
            is_runnable = entry.IsRunnable,
            runtime_backend = entry.RuntimeBackend,
            local_path = entry.LocalPath,
            quarantine_path = entry.QuarantinePath,
            last_verified_utc = entry.LastVerifiedUtc,
            last_runtime_check_utc = entry.LastRuntimeCheckUtc
        });
    }

    private async Task<object> HandleImportLocalModelAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        if (!body.TryGetProperty("path", out var pathElement))
        {
            return CreateErrorResponse("import_local_model", "INVALID_INPUT", "Missing 'path' parameter.");
        }

        var path = pathElement.GetString();
        if (string.IsNullOrWhiteSpace(path))
        {
            return CreateErrorResponse("import_local_model", "INVALID_INPUT", "Path parameter is empty.");
        }

        var entry = _services.Catalog.ImportLocalModel(path);
        return CreateSuccessResponse("import_local_model", new
        {
            model_id = entry.ModelId,
            display_name = entry.DisplayName,
            local_path = entry.LocalPath,
            status = entry.Status,
            health_status = entry.HealthStatus,
            health_reason = entry.HealthReason
        });
    }

    private async Task<object> HandleVerifyModelAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var modelId = body.TryGetProperty("model_id", out var modelIdElement) ? modelIdElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return CreateErrorResponse("verify_model", "INVALID_INPUT", "Missing 'model_id' parameter.");
        }

        var entry = _services.Catalog.VerifyModel(modelId);
        if (entry is null)
        {
            return CreateErrorResponse("verify_model", "MODEL_NOT_FOUND", $"Model '{modelId}' was not found.");
        }

        return CreateSuccessResponse("verify_model", new
        {
            model_id = entry.ModelId,
            status = entry.Status,
            health_status = entry.HealthStatus,
            health_reason = entry.HealthReason,
            local_path = entry.LocalPath,
            quarantine_path = entry.QuarantinePath
        });
    }

    private async Task<object> HandleRepairModelAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var modelId = body.TryGetProperty("model_id", out var modelIdElement) ? modelIdElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return CreateErrorResponse("repair_model", "INVALID_INPUT", "Missing 'model_id' parameter.");
        }

        var entry = await _services.Catalog.RepairModelAsync(modelId);
        if (entry is null)
        {
            return CreateErrorResponse("repair_model", "MODEL_NOT_FOUND", $"Model '{modelId}' was not found.");
        }

        return CreateSuccessResponse("repair_model", new
        {
            model_id = entry.ModelId,
            status = entry.Status,
            health_status = entry.HealthStatus,
            health_reason = entry.HealthReason,
            local_path = entry.LocalPath,
            quarantine_path = entry.QuarantinePath
        });
    }

    private async Task<object> HandleQuarantineModelAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var modelId = body.TryGetProperty("model_id", out var modelIdElement) ? modelIdElement.GetString() : null;
        var reason = body.TryGetProperty("reason", out var reasonElement) ? reasonElement.GetString() : string.Empty;
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return CreateErrorResponse("quarantine_model", "INVALID_INPUT", "Missing 'model_id' parameter.");
        }

        var entry = _services.Catalog.QuarantineModel(modelId, reason ?? string.Empty);
        if (entry is null)
        {
            return CreateErrorResponse("quarantine_model", "MODEL_NOT_FOUND", $"Model '{modelId}' was not found.");
        }

        return CreateSuccessResponse("quarantine_model", new
        {
            model_id = entry.ModelId,
            status = entry.Status,
            health_status = entry.HealthStatus,
            health_reason = entry.HealthReason,
            local_path = entry.LocalPath,
            quarantine_path = entry.QuarantinePath
        });
    }

    private async Task<object> HandlePreloadModelAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var modelId = body.TryGetProperty("model_id", out var modelIdElement) ? modelIdElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return CreateErrorResponse("preload_model", "INVALID_INPUT", "Missing 'model_id' parameter.");
        }

        var item = await _services.Library.PreloadModelAsync(modelId);
        if (item is null)
        {
            var catalogEntry = _services.Catalog.FindById(modelId);
            if (catalogEntry is not null)
            {
                return CreateErrorResponse("preload_model", string.Equals(catalogEntry.HealthStatus, "blocked_by_memory_budget", StringComparison.OrdinalIgnoreCase) ? "BLOCKED_BY_MEMORY_BUDGET" : "MODEL_NOT_READY", string.IsNullOrWhiteSpace(catalogEntry.RunnableReason) ? $"Model '{modelId}' is not ready to preload." : catalogEntry.RunnableReason);
            }

            return CreateErrorResponse("preload_model", "MODEL_NOT_FOUND", $"Model '{modelId}' was not found.");
        }

        return CreateSuccessResponse("preload_model", new
        {
            instance_id = item.InstanceId,
            model_id = item.ModelId,
            load_state = item.LoadState,
            is_active = item.IsActive,
            is_pinned = item.IsPinned,
            primary_mode = item.PrimaryMode
        });
    }

    private async Task<object> HandleActivateLocalModelAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var modelId = body.TryGetProperty("model_id", out var modelIdElement) ? modelIdElement.GetString() : null;
        var role = body.TryGetProperty("role", out var roleElement) ? roleElement.GetString() : "thinking";
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return CreateErrorResponse("activate_local_model", "INVALID_INPUT", "Missing 'model_id' parameter.");
        }

        var item = await _services.Library.ActivateModelAsync(modelId, string.IsNullOrWhiteSpace(role) ? "thinking" : role!);
        if (item is null)
        {
            var catalogEntry = _services.Catalog.FindById(modelId);
            if (catalogEntry is not null)
            {
                var errorCode =
                    string.Equals(catalogEntry.AdmissionStatus, "unsupported", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(catalogEntry.RuntimeBackend, "unsupported-gguf", StringComparison.OrdinalIgnoreCase)
                        ? "MODEL_UNSUPPORTED"
                        : string.Equals(catalogEntry.HealthStatus, "blocked_by_memory_budget", StringComparison.OrdinalIgnoreCase)
                            ? "BLOCKED_BY_MEMORY_BUDGET"
                            : "MODEL_NOT_READY";
                return CreateErrorResponse("activate_local_model", errorCode, string.IsNullOrWhiteSpace(catalogEntry.RunnableReason) ? $"Model '{modelId}' is not ready to activate." : catalogEntry.RunnableReason);
            }

            return CreateErrorResponse("activate_local_model", "MODEL_NOT_FOUND", $"Model '{modelId}' was not found.");
        }

        return CreateSuccessResponse("activate_local_model", new
        {
            instance_id = item.InstanceId,
            model_id = item.ModelId,
            load_state = item.LoadState,
            is_active = item.IsActive,
            primary_mode = item.PrimaryMode,
            capabilities = item.Capabilities
        });
    }

    private async Task<object> HandleActivateInstanceAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var instanceId = body.TryGetProperty("instance_id", out var instanceElement) ? instanceElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return CreateErrorResponse("activate_instance", "INVALID_INPUT", "Missing 'instance_id' parameter.");
        }

        await Task.CompletedTask;
        var item = _services.Library.SetInstanceActive(instanceId, true);
        if (item is null)
        {
            return CreateErrorResponse("activate_instance", "INSTANCE_NOT_FOUND", $"Instance '{instanceId}' was not found.");
        }

        return CreateSuccessResponse("activate_instance", new
        {
            instance_id = item.InstanceId,
            model_id = item.ModelId,
            load_state = item.LoadState,
            is_active = item.IsActive,
            primary_mode = item.PrimaryMode
        });
    }

    private async Task<object> HandleDeactivateLocalModelAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var instanceId = body.TryGetProperty("instance_id", out var instanceElement) ? instanceElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return CreateErrorResponse("deactivate_local_model", "INVALID_INPUT", "Missing 'instance_id' parameter.");
        }

        var item = _services.Library.DeactivateInstance(instanceId);
        if (item is null)
        {
            return CreateErrorResponse("deactivate_local_model", "INSTANCE_NOT_FOUND", $"Instance '{instanceId}' was not found.");
        }

        return CreateSuccessResponse("deactivate_local_model", new
        {
            instance_id = item.InstanceId,
            load_state = item.LoadState,
            is_active = item.IsActive
        });
    }

    private async Task<object> HandleReloadModelAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var instanceId = body.TryGetProperty("instance_id", out var instanceElement) ? instanceElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return CreateErrorResponse("reload_model", "INVALID_INPUT", "Missing 'instance_id' parameter.");
        }

        var item = await _services.Library.ReloadInstanceAsync(instanceId);
        if (item is null)
        {
            return CreateErrorResponse("reload_model", "INSTANCE_NOT_FOUND", $"Instance '{instanceId}' was not found.");
        }

        return CreateSuccessResponse("reload_model", new
        {
            instance_id = item.InstanceId,
            load_state = item.LoadState,
            is_active = item.IsActive,
            last_loaded_utc = item.LastLoadedUtc
        });
    }

    private async Task<object> HandleDeleteInstanceAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var instanceId = body.TryGetProperty("instance_id", out var instanceElement) ? instanceElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return CreateErrorResponse("delete_instance", "INVALID_INPUT", "Missing 'instance_id' parameter.");
        }

        var existing = _services.Library.FindInstance(instanceId);
        if (existing is null)
        {
            return CreateErrorResponse("delete_instance", "INSTANCE_NOT_FOUND", $"Instance '{instanceId}' was not found.");
        }

        _services.Library.DeleteInstance(instanceId);
        return CreateSuccessResponse("delete_instance", new
        {
            instance_id = instanceId,
            deleted = true
        });
    }

    private async Task<object> HandleSetInstanceTokensAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var instanceId = body.TryGetProperty("instance_id", out var instanceElement) ? instanceElement.GetString() : null;
        var value = body.TryGetProperty("value", out var valueElement) && valueElement.TryGetInt32(out var parsedValue) ? parsedValue : 0;
        if (string.IsNullOrWhiteSpace(instanceId) || value <= 0)
        {
            return CreateErrorResponse("set_instance_tokens", "INVALID_INPUT", "Both 'instance_id' and a positive 'value' are required.");
        }

        var item = _services.Library.UpdateInstanceTokens(instanceId, value);
        if (item is null)
        {
            return CreateErrorResponse("set_instance_tokens", "INSTANCE_NOT_FOUND", $"Instance '{instanceId}' was not found.");
        }

        return CreateSuccessResponse("set_instance_tokens", new
        {
            instance_id = item.InstanceId,
            value = item.MaxTokens
        });
    }

    private async Task<object> HandleSetInstanceMemoryCapacityAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var instanceId = body.TryGetProperty("instance_id", out var instanceElement) ? instanceElement.GetString() : null;
        var value = body.TryGetProperty("value", out var valueElement) && valueElement.TryGetInt64(out var parsedValue) ? parsedValue : 0;
        if (string.IsNullOrWhiteSpace(instanceId) || value <= 0)
        {
            return CreateErrorResponse("set_instance_memory_capacity", "INVALID_INPUT", "Both 'instance_id' and a positive 'value' are required.");
        }

        var item = _services.Library.UpdateInstanceMemoryCapacity(instanceId, value);
        if (item is null)
        {
            return CreateErrorResponse("set_instance_memory_capacity", "INSTANCE_NOT_FOUND", $"Instance '{instanceId}' was not found.");
        }

        return CreateSuccessResponse("set_instance_memory_capacity", new
        {
            instance_id = item.InstanceId,
            value = item.MemoryReservedMb
        });
    }

    private async Task<object> HandleSetInstanceModeAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var instanceId = body.TryGetProperty("instance_id", out var instanceElement) ? instanceElement.GetString() : null;
        var mode = body.TryGetProperty("mode", out var modeElement) ? modeElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(mode))
        {
            return CreateErrorResponse("set_instance_mode", "INVALID_INPUT", "Both 'instance_id' and 'mode' are required.");
        }

        var item = _services.Library.UpdateInstanceMode(instanceId, mode);
        if (item is null)
        {
            return CreateErrorResponse("set_instance_mode", "INSTANCE_NOT_FOUND", $"Instance '{instanceId}' was not found.");
        }

        return CreateSuccessResponse("set_instance_mode", new
        {
            instance_id = item.InstanceId,
            primary_mode = item.PrimaryMode
        });
    }

    private object HandleGetInstanceConsole(HttpListenerRequest request)
    {
        var instanceId = request.QueryString["instance_id"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return CreateErrorResponse("get_instance_console", "INVALID_INPUT", "Missing 'instance_id' query parameter.");
        }

        var item = _services.Library.FindInstance(instanceId);
        if (item is null)
        {
            return CreateErrorResponse("get_instance_console", "INSTANCE_NOT_FOUND", $"Instance '{instanceId}' was not found.");
        }

        return CreateSuccessResponse("get_instance_console", new
        {
            instance_id = item.InstanceId,
            console = item.ConsoleEntries.Select(entry => new
            {
                timestamp_utc = entry.TimestampUtc,
                source = entry.Source,
                text = entry.Text
            })
        });
    }

    private async Task<object> HandlePinModelAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var modelId = body.TryGetProperty("model_id", out var modelIdElement) ? modelIdElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return CreateErrorResponse("pin_model", "INVALID_INPUT", "Missing 'model_id' parameter.");
        }

        var instanceId = body.TryGetProperty("instance_id", out var instanceIdElement) ? instanceIdElement.GetString() : null;
        var item = !string.IsNullOrWhiteSpace(instanceId)
            ? _services.Library.PinInstance(instanceId!, true)
            : _services.Library.ResolveInstanceForEndpoint("thinking", explicitModelId: modelId) is { } resolved
                ? _services.Library.PinInstance(resolved.InstanceId, true)
                : null;
        if (item is null)
        {
            return CreateErrorResponse("pin_model", "MODEL_NOT_FOUND", $"Model '{modelId}' was not found.");
        }

        return CreateSuccessResponse("pin_model", new
        {
            instance_id = item.InstanceId,
            model_id = item.ModelId,
            is_pinned = item.IsPinned
        });
    }

    private async Task<object> HandleUnpinModelAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var modelId = body.TryGetProperty("model_id", out var modelIdElement) ? modelIdElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return CreateErrorResponse("unpin_model", "INVALID_INPUT", "Missing 'model_id' parameter.");
        }

        var instanceId = body.TryGetProperty("instance_id", out var instanceIdElement) ? instanceIdElement.GetString() : null;
        var item = !string.IsNullOrWhiteSpace(instanceId)
            ? _services.Library.PinInstance(instanceId!, false)
            : _services.Library.ResolveInstanceForEndpoint("thinking", explicitModelId: modelId) is { } resolved
                ? _services.Library.PinInstance(resolved.InstanceId, false)
                : null;
        if (item is null)
        {
            return CreateErrorResponse("unpin_model", "MODEL_NOT_FOUND", $"Model '{modelId}' was not found.");
        }

        return CreateSuccessResponse("unpin_model", new
        {
            instance_id = item.InstanceId,
            model_id = item.ModelId,
            is_pinned = item.IsPinned
        });
    }

    private async Task<object> HandleSetRuntimeRolesAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var role = body.TryGetProperty("role", out var roleElement) ? roleElement.GetString() : null;
        var modelId = body.TryGetProperty("model_id", out var modelIdElement) ? modelIdElement.GetString() : null;
        var instanceId = body.TryGetProperty("instance_id", out var instanceIdElement) ? instanceIdElement.GetString() : null;
        var routingMode = body.TryGetProperty("routing_mode", out var routingModeElement) ? routingModeElement.GetString() : null;
        var reservedMemory = body.TryGetProperty("reserved_memory", out var reservedElement) && reservedElement.TryGetInt32(out var reservedParsed) ? reservedParsed : (int?)null;
        var maxParallel = body.TryGetProperty("max_parallel", out var parallelElement) && parallelElement.TryGetInt32(out var parallelParsed) ? parallelParsed : (int?)null;
        var queueLimit = body.TryGetProperty("queue_limit", out var queueElement) && queueElement.TryGetInt32(out var queueParsed) ? queueParsed : (int?)null;

        if (string.IsNullOrWhiteSpace(role))
        {
            return CreateErrorResponse("set_runtime_roles", "INVALID_INPUT", "Missing 'role' parameter.");
        }

        if (!string.IsNullOrWhiteSpace(instanceId) && !string.IsNullOrWhiteSpace(routingMode))
        {
            _services.Library.UpdateInstanceMode(instanceId!, routingMode!);
        }
        else if (!string.IsNullOrWhiteSpace(modelId))
        {
            await _services.Library.AssignModelToRoleAsync(role!, modelId!);
        }

        var updated = _services.Library.UpdateRoleSettings(role!, modelId, routingMode, reservedMemory, maxParallel, queueLimit);
        if (updated is null)
        {
            return CreateErrorResponse("set_runtime_roles", "ROLE_NOT_FOUND", $"Role '{role}' was not found.");
        }

        return CreateSuccessResponse("set_runtime_roles", new
        {
            role = updated.RoleName,
            assigned_instance_id = updated.AssignedInstanceId,
            assigned_model_id = updated.AssignedModelId,
            assigned_model_display_name = updated.AssignedModelDisplayName,
            routing_mode = updated.RoutingMode,
            reserved_memory = updated.ReservedMemory,
            max_parallel = updated.MaxParallel,
            queue_limit = updated.QueueLimit,
            live_status = updated.LiveStatus
        });
    }

    private async Task<object> HandlePullModelAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var repo = body.TryGetProperty("repo", out var repoElement) ? repoElement.GetString() : null;
        var filename = body.TryGetProperty("filename", out var filenameElement) ? filenameElement.GetString() : null;
        var revision = body.TryGetProperty("revision", out var revisionElement) ? revisionElement.GetString() : "main";

        if (string.IsNullOrWhiteSpace(repo) || string.IsNullOrWhiteSpace(filename))
        {
            return CreateErrorResponse("pull_model", "INVALID_INPUT", "Both 'repo' and 'filename' are required.");
        }

        var job = _services.Downloads.StartDownload(repo, filename, revision ?? "main");
        return CreateSuccessResponse("pull_model", new
        {
            download_id = job.DownloadId,
            status = job.Status,
            repo = job.Repo,
            filename = job.Filename,
            destination_path = job.DestinationPath,
            log_path = _services.Paths.GetDownloadLogPath(job.DownloadId)
        });
    }

    private object HandleGetLogs(HttpListenerRequest request)
    {
        var category = request.QueryString["category"] ?? string.Empty;
        var downloadId = request.QueryString["download_id"] ?? string.Empty;
        var limit = int.TryParse(request.QueryString["limit"], out var parsedLimit) ? Math.Max(1, parsedLimit) : 100;

        if (!string.IsNullOrWhiteSpace(downloadId))
        {
            var path = _services.Paths.GetDownloadLogPath(downloadId);
            var lines = File.Exists(path)
                ? File.ReadLines(path).TakeLast(limit).ToList()
                : new List<string>();

            return CreateSuccessResponse("get_logs", new
            {
                category = string.IsNullOrWhiteSpace(category) ? "downloads" : category,
                download_id = downloadId,
                lines,
                path
            });
        }

        var events = _services.Logs.GetEvents()
            .Where(log => string.IsNullOrWhiteSpace(category) || string.Equals(log.Source, category, StringComparison.OrdinalIgnoreCase))
            .TakeLast(limit)
            .Select(log => new
            {
                timestamp_ms = log.TimestampMs,
                level = log.Level,
                source = log.Source,
                scope_id = log.ScopeId,
                message = log.Message
            });

        return CreateSuccessResponse("get_logs", new
        {
            category,
            lines = events
        });
    }

    private async Task<object> HandleThinkingAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var prompt = body.TryGetProperty("prompt", out var promptElement) ? promptElement.GetString() : null;
        var modelId = body.TryGetProperty("model_id", out var modelElement) ? modelElement.GetString() : null;
        var instanceId = body.TryGetProperty("instance_id", out var instanceElement) ? instanceElement.GetString() : null;
        var maxTokens = body.TryGetProperty("max_tokens", out var tokensElement) && tokensElement.TryGetInt32(out var parsedTokens)
            ? parsedTokens
            : 1200;
        var maxThreads = body.TryGetProperty("max_threads", out var threadsElement) && threadsElement.TryGetInt32(out var parsedThreads) && parsedThreads > 0
            ? parsedThreads
            : (int?)null;
        var maxMemoryMb = body.TryGetProperty("max_memory_mb", out var memoryElement) && memoryElement.TryGetInt32(out var parsedMemory) && parsedMemory > 0
            ? parsedMemory
            : (int?)null;

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return CreateErrorResponse("thinking", "INVALID_INPUT", "Missing 'prompt' parameter.");
        }

        var result = await _services.Runtime.RunThinkingAsync(prompt, modelId, maxTokens, instanceId, maxThreads, maxMemoryMb);
        return BuildThinkingFinalResponse("thinking", result, maxTokens);
    }

    private async Task<object> HandleThinkingRawAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var prompt = body.TryGetProperty("prompt", out var promptElement) ? promptElement.GetString() : null;
        var modelId = body.TryGetProperty("model_id", out var modelElement) ? modelElement.GetString() : null;
        var instanceId = body.TryGetProperty("instance_id", out var instanceElement) ? instanceElement.GetString() : null;
        var maxTokens = body.TryGetProperty("max_tokens", out var tokensElement) && tokensElement.TryGetInt32(out var parsedTokens)
            ? parsedTokens
            : 1200;
        var maxThreads = body.TryGetProperty("max_threads", out var threadsElement) && threadsElement.TryGetInt32(out var parsedThreads) && parsedThreads > 0
            ? parsedThreads
            : (int?)null;
        var maxMemoryMb = body.TryGetProperty("max_memory_mb", out var memoryElement) && memoryElement.TryGetInt32(out var parsedMemory) && parsedMemory > 0
            ? parsedMemory
            : (int?)null;

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return CreateErrorResponse("thinking-raw", "INVALID_INPUT", "Missing 'prompt' parameter.");
        }

        var result = await _services.Runtime.RunThinkingAsync(prompt, modelId, maxTokens, instanceId, maxThreads, maxMemoryMb);
        return BuildThinkingRawResponse("thinking-raw", result, maxTokens);
    }

    private async Task HandleThinkingStreamAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var prompt = body.TryGetProperty("prompt", out var promptElement) ? promptElement.GetString() : null;
        var modelId = body.TryGetProperty("model_id", out var modelElement) ? modelElement.GetString() : null;
        var instanceId = body.TryGetProperty("instance_id", out var instanceElement) ? instanceElement.GetString() : null;
        var maxTokens = body.TryGetProperty("max_tokens", out var tokensElement) && tokensElement.TryGetInt32(out var parsedTokens)
            ? parsedTokens
            : 1200;
        var maxThreads = body.TryGetProperty("max_threads", out var threadsElement) && threadsElement.TryGetInt32(out var parsedThreads) && parsedThreads > 0
            ? parsedThreads
            : (int?)null;
        var maxMemoryMb = body.TryGetProperty("max_memory_mb", out var memoryElement) && memoryElement.TryGetInt32(out var parsedMemory) && parsedMemory > 0
            ? parsedMemory
            : (int?)null;

        if (string.IsNullOrWhiteSpace(prompt))
        {
            response.StatusCode = 400;
            await WriteObjectResponseAsync(response, CreateErrorResponse("thinking-stream", "INVALID_INPUT", "Missing 'prompt' parameter."));
            return;
        }

        response.StatusCode = 200;
        response.ContentType = "application/x-ndjson";
        response.SendChunked = true;

        await using var writer = new StreamWriter(response.OutputStream);
        var progress = new IntegrationRuntimeStreamProgress(writer);
        var result = await _services.Runtime.RunThinkingAsync(prompt, modelId, maxTokens, instanceId, progress, maxThreads, maxMemoryMb);
        await writer.WriteLineAsync(JsonSerializer.Serialize(new
        {
            type = "final",
            data = BuildThinkingRawResponse("thinking-stream", result, maxTokens)
        }, WorkflowJsonOptions));
        await writer.FlushAsync();
        response.Close();
    }

    private async Task<object> HandleInstructAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var prompt = body.TryGetProperty("prompt", out var promptElement) ? promptElement.GetString() : null;
        var modelId = body.TryGetProperty("model_id", out var modelElement) ? modelElement.GetString() : null;
        var instanceId = body.TryGetProperty("instance_id", out var instanceElement) ? instanceElement.GetString() : null;
        var maxTokens = body.TryGetProperty("max_tokens", out var tokensElement) && tokensElement.TryGetInt32(out var parsedTokens)
            ? parsedTokens
            : 1200;
        var maxThreads = body.TryGetProperty("max_threads", out var threadsElement) && threadsElement.TryGetInt32(out var parsedThreads) && parsedThreads > 0
            ? parsedThreads
            : (int?)null;
        var maxMemoryMb = body.TryGetProperty("max_memory_mb", out var memoryElement) && memoryElement.TryGetInt32(out var parsedMemory) && parsedMemory > 0
            ? parsedMemory
            : (int?)null;

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return CreateErrorResponse("instruct", "INVALID_INPUT", "Missing 'prompt' parameter.");
        }

        var result = await _services.Runtime.RunInstructAsync(prompt, modelId, maxTokens, instanceId, maxThreads, maxMemoryMb);
        return BuildRuntimeResponse("instruct", result, maxTokens);
    }

    private object BuildRuntimeResponse(string toolName, NexusCentral_Avalonia.Models.RuntimeInvocationResult result, int maxTokens)
    {
        return result.Ok
            ? CreateSuccessResponse(toolName, new
            {
                model_id = result.ModelId,
                backend = result.Backend,
                output = result.Output,
                raw_output = result.RawOutput,
                finish_reason = result.FinishReason,
                tokens_generated = result.TokensGenerated,
                elapsed_ms = result.ElapsedMs,
                model_path = result.ModelPath,
                prompt = result.Prompt,
                max_tokens = maxTokens
            })
            : CreateErrorResponse(toolName, string.IsNullOrWhiteSpace(result.ErrorCode) ? "RUNTIME_ERROR" : result.ErrorCode, result.Error);
    }

    private object BuildThinkingFinalResponse(string toolName, NexusCentral_Avalonia.Models.RuntimeInvocationResult result, int maxTokens)
    {
        if (!result.Ok)
        {
            return CreateErrorResponse(toolName, string.IsNullOrWhiteSpace(result.ErrorCode) ? "RUNTIME_ERROR" : result.ErrorCode, result.Error);
        }

        var separated = ThinkingOutputParser.Separate(result.RawOutput, result.Output);
        return CreateSuccessResponse(toolName, new
        {
            model_id = result.ModelId,
            backend = result.Backend,
            output = separated.FinalText,
            finish_reason = result.FinishReason,
            tokens_generated = result.TokensGenerated,
            elapsed_ms = result.ElapsedMs,
            model_path = result.ModelPath,
            prompt = result.Prompt,
            max_tokens = maxTokens
        });
    }

    private object BuildThinkingRawResponse(string toolName, NexusCentral_Avalonia.Models.RuntimeInvocationResult result, int maxTokens)
    {
        if (!result.Ok)
        {
            return CreateErrorResponse(toolName, string.IsNullOrWhiteSpace(result.ErrorCode) ? "RUNTIME_ERROR" : result.ErrorCode, result.Error);
        }

        var rawOutput = ThinkingOutputParser.GetRawOutput(result.RawOutput, result.Output);
        return CreateSuccessResponse(toolName, new
        {
            model_id = result.ModelId,
            backend = result.Backend,
            output = rawOutput,
            raw_output = rawOutput,
            finish_reason = result.FinishReason,
            tokens_generated = result.TokensGenerated,
            elapsed_ms = result.ElapsedMs,
            model_path = result.ModelPath,
            prompt = result.Prompt,
            max_tokens = maxTokens
        });
    }

    private async Task<object> HandleGetVisibleElementsAsync()
    {
        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            var workflows = _services.WorkflowViewModel;
            if (chat is null)
            {
                return CreateErrorResponse("get_visible_elements", "CHAT_NOT_READY", "Chat view model is not available.");
            }

            var isChatTab = IsChatTabName(_services.AppState.CurrentTab);
            var isModelsTab = string.Equals(_services.AppState.CurrentTab, "Models", StringComparison.OrdinalIgnoreCase);
            var isWorkflowTab = ((string.Equals(_services.AppState.CurrentTab, "Workflows", StringComparison.OrdinalIgnoreCase)) || (string.Equals(_services.AppState.CurrentTab, "Templates", StringComparison.OrdinalIgnoreCase)));
            var isAgentsTab = string.Equals(_services.AppState.CurrentTab, "Agents", StringComparison.OrdinalIgnoreCase);
            var elements = new List<object>
            {
                new
                {
                    element_id = ChatViewModel.AgentDropdownElementId,
                    type = "combobox",
                    label = "Chat agent dropdown",
                    visible = isChatTab && chat is not null && chat.IsToolElementVisible(ChatViewModel.AgentDropdownElementId),
                    enabled = true,
                    selected = !string.IsNullOrWhiteSpace(chat?.SelectedAgent?.Id),
                    highlighted = _services.UiAutomation.IsHighlighted(ChatViewModel.AgentDropdownElementId),
                    expanded = _services.UiAutomation.IsExpanded(ChatViewModel.AgentDropdownElementId)
                },
                new
                {
                    element_id = ChatViewModel.AttachButtonElementId,
                    type = "button",
                    label = "Chat attachment button",
                    visible = isChatTab && chat is not null && chat.IsToolElementVisible(ChatViewModel.AttachButtonElementId),
                    enabled = chat?.CanAttachArtifacts ?? false,
                    selected = chat?.ShowArtifactInputBox ?? false,
                    highlighted = chat?.IsAttachButtonHighlighted ?? false,
                    expanded = _services.UiAutomation.IsExpanded(ChatViewModel.AttachButtonElementId)
                },
                new
                {
                    element_id = ChatViewModel.InputElementId,
                    type = "textbox",
                    label = "Chat input",
                    visible = isChatTab && chat is not null && chat.IsToolElementVisible(ChatViewModel.InputElementId),
                    enabled = chat is not null && !chat.IsGenerating,
                    selected = false,
                    highlighted = chat?.IsInputHighlighted ?? false,
                    expanded = false
                },
                new
                {
                    element_id = ChatViewModel.ArtifactInputsElementId,
                    type = "textbox",
                    label = "Chat artifact inputs",
                    visible = isChatTab && chat is not null && chat.IsToolElementVisible(ChatViewModel.ArtifactInputsElementId),
                    enabled = chat?.ShowArtifactInputBox ?? false,
                    selected = !string.IsNullOrWhiteSpace(chat?.ArtifactInputsText),
                    highlighted = _services.UiAutomation.IsHighlighted(ChatViewModel.ArtifactInputsElementId),
                    expanded = false
                },
                new
                {
                    element_id = ChatViewModel.MaxTokensElementId,
                    type = "slider",
                    label = "Model instance max tokens (Models)",
                    visible = isModelsTab && _services.Library.GetLoadedInstances().Count > 0,
                    enabled = true,
                    selected = false,
                    highlighted = chat?.IsMaxTokensHighlighted ?? false,
                    expanded = false,
                    owner_tab = "Models",
                    location = "models.loaded_instances",
                    compatibility_alias = true
                },
                new
                {
                    element_id = ChatViewModel.SendButtonElementId,
                    type = "button",
                    label = "Send chat message",
                    visible = isChatTab && chat is not null && chat.IsToolElementVisible(ChatViewModel.SendButtonElementId),
                    enabled = chat is not null && !chat.IsGenerating,
                    selected = false,
                    highlighted = chat?.IsSendButtonHighlighted ?? false,
                    expanded = false
                },
                new
                {
                    element_id = ChatViewModel.ReloadButtonElementId,
                    type = "button",
                    label = "Reload chat session",
                    visible = isChatTab && chat is not null && chat.IsToolElementVisible(ChatViewModel.ReloadButtonElementId),
                    enabled = true,
                    selected = false,
                    highlighted = chat?.IsReloadButtonHighlighted ?? false,
                    expanded = false
                }
            };

            if (isWorkflowTab && workflows?.CurrentWorkflow is not null)
            {
                elements.AddRange(new[]
                {
                    new
                    {
                        element_id = WorkflowViewModel.WorkflowSelectorElementId,
                        type = "combobox",
                        label = "Workflow selector",
                        visible = true,
                        enabled = true,
                        selected = workflows.SelectedLibraryWorkflow is not null,
                        highlighted = false,
                        expanded = false
                    },
                    new
                    {
                        element_id = WorkflowViewModel.SearchElementId,
                        type = "textbox",
                        label = "Workflow search",
                        visible = true,
                        enabled = true,
                        selected = !string.IsNullOrWhiteSpace(workflows.SearchText),
                        highlighted = false,
                        expanded = false
                    },
                    new
                    {
                        element_id = WorkflowViewModel.BundleIdElementId,
                        type = "textbox",
                        label = "Workflow bundle id",
                        visible = true,
                        enabled = true,
                        selected = !string.IsNullOrWhiteSpace(workflows.BundleIdText),
                        highlighted = false,
                        expanded = false
                    },
                    new
                    {
                        element_id = WorkflowViewModel.RunWorkflowElementId,
                        type = "button",
                        label = "Run workflow",
                        visible = true,
                        enabled = true,
                        selected = false,
                        highlighted = false,
                        expanded = false
                    },
                    new
                    {
                        element_id = WorkflowViewModel.StopWorkflowElementId,
                        type = "button",
                        label = "Stop workflow",
                        visible = true,
                        enabled = !string.IsNullOrWhiteSpace(workflows.CurrentWorkflow.ActiveRunId),
                        selected = false,
                        highlighted = false,
                        expanded = false
                    },
                    new
                    {
                        element_id = WorkflowViewModel.SaveWorkflowElementId,
                        type = "button",
                        label = "Save workflow",
                        visible = true,
                        enabled = true,
                        selected = false,
                        highlighted = false,
                        expanded = false
                    },
                    new
                    {
                        element_id = WorkflowViewModel.SaveBundleElementId,
                        type = "button",
                        label = "Save workflow bundle",
                        visible = true,
                        enabled = true,
                        selected = false,
                        highlighted = false,
                        expanded = false
                    },
                    new
                    {
                        element_id = WorkflowViewModel.LoadBundleElementId,
                        type = "button",
                        label = "Load workflow bundle",
                        visible = true,
                        enabled = true,
                        selected = false,
                        highlighted = false,
                        expanded = false
                    },
                    new
                    {
                        element_id = WorkflowViewModel.AddInputElementId,
                        type = "button",
                        label = "Add workflow input",
                        visible = true,
                        enabled = true,
                        selected = false,
                        highlighted = false,
                        expanded = false
                    },
                    new
                    {
                        element_id = WorkflowViewModel.AddAgentStepElementId,
                        type = "button",
                        label = "Add agent step",
                        visible = true,
                        enabled = true,
                        selected = false,
                        highlighted = false,
                        expanded = false
                    },
                    new
                    {
                        element_id = WorkflowViewModel.AddToolStepElementId,
                        type = "button",
                        label = "Add tool step",
                        visible = true,
                        enabled = true,
                        selected = false,
                        highlighted = false,
                        expanded = false
                    }
                });

                elements.AddRange(workflows.CurrentWorkflow.Steps.Select(step => new
                {
                    element_id = WorkflowViewModel.BuildStepElementId(step.Id),
                    type = "workflow-step",
                    label = step.Name,
                    visible = true,
                    enabled = step.Enabled,
                    selected = step.IsExpanded,
                    highlighted = false,
                    expanded = step.IsExpanded
                }));

                elements.AddRange(workflows.CurrentWorkflow.Steps.Select(step => new
                {
                    element_id = WorkflowViewModel.BuildStepRunElementId(step.Id),
                    type = "button",
                    label = $"Run {step.Name}",
                    visible = true,
                    enabled = step.Enabled,
                    selected = false,
                    highlighted = false,
                    expanded = false
                }));
            }

            if (isChatTab && chat is not null && chat.IsModelDropdownOpen)
            {
                elements.AddRange(chat.AvailableModels.Select(option => new
                {
                    element_id = BuildOptionElementId(option.InstanceId),
                    type = "option",
                    label = option.DisplayName,
                    visible = true,
                    enabled = option.IsRunnable || string.IsNullOrWhiteSpace(option.ModelId),
                    selected = string.Equals(option.InstanceId, chat.SelectedModel?.InstanceId, StringComparison.OrdinalIgnoreCase),
                    highlighted = false,
                    expanded = false
                }));
            }

            if (isChatTab && chat is not null && _services.UiAutomation.IsExpanded(ChatViewModel.WrapperDropdownElementId))
            {
                elements.AddRange(chat.AvailableWrappers.Select(wrapper => new
                {
                    element_id = BuildWrapperOptionElementId(wrapper.Id),
                    type = "option",
                    label = wrapper.Name,
                    visible = true,
                    enabled = wrapper.IsAvailable,
                    selected = string.Equals(wrapper.Id, chat.SelectedWrapper?.Id, StringComparison.OrdinalIgnoreCase),
                    highlighted = false,
                    expanded = false
                }));
            }

            if (isModelsTab)
            {
                elements.AddRange(_services.Library.GetLoadedInstances().Select(instance => new
                {
                    element_id = BuildInstanceElementId(instance.InstanceId),
                    type = "instance-card",
                    label = instance.DisplayName,
                    visible = true,
                    enabled = true,
                    selected = instance.IsActive,
                    highlighted = false,
                    expanded = false
                }));
            }

            return CreateSuccessResponse("get_visible_elements", new
            {
                current_tab = _services.AppState.CurrentTab,
                elements
            });
        });
    }

    private async Task<object> HandleGetFormStateAsync()
    {
        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            var workflows = _services.WorkflowViewModel;
            if (chat is null)
            {
                return CreateErrorResponse("get_form_state", "CHAT_NOT_READY", "Chat view model is not available.");
            }

            return CreateSuccessResponse("get_form_state", new
            {
                current_tab = _services.AppState.CurrentTab,
                forms = new
                {
                    router = new
                    {
                        loaded_instances = _services.Library.GetLoadedInstances().Select(instance => new
                        {
                            instance_id = instance.InstanceId,
                            element_id = BuildInstanceElementId(instance.InstanceId),
                            model_id = instance.ModelId,
                            display_name = instance.DisplayName,
                            is_active = instance.IsActive,
                            is_pinned = instance.IsPinned,
                            primary_mode = instance.PrimaryMode,
                            capabilities = instance.Capabilities,
                            max_tokens = instance.MaxTokens,
                            memory_reserved_mb = instance.MemoryReservedMb,
                            memory_actual_mb = instance.MemoryActualMb,
                            load_state = instance.LoadState
                        })
                    },
                    chat = new
                    {
                        execution = new
                        {
                            mode = chat.SelectedExecutionMode,
                            template_id = chat.SelectedTemplate?.Id ?? string.Empty,
                            template_name = chat.SelectedTemplate?.Name ?? string.Empty,
                            wrapper_id = chat.SelectedWrapper?.Id ?? string.Empty,
                            wrapper_name = chat.SelectedWrapper?.Name ?? string.Empty,
                            artifact_inputs_text = chat.ArtifactInputsText
                        },
                        agent_dropdown = new
                        {
                            element_id = ChatViewModel.AgentDropdownElementId,
                            visible = chat.ShowAgentSelector,
                            expanded = _services.UiAutomation.IsExpanded(ChatViewModel.AgentDropdownElementId),
                            highlighted = _services.UiAutomation.IsHighlighted(ChatViewModel.AgentDropdownElementId),
                            selected_agent_id = chat.SelectedAgent?.Id ?? string.Empty,
                            selected_label = chat.SelectedAgent?.Name ?? string.Empty,
                            options = chat.AvailableAgents.Select(wrapper => new
                            {
                                item_id = wrapper.Id,
                                element_id = BuildWrapperOptionElementId(wrapper.Id),
                                label = wrapper.Name,
                                is_available = wrapper.IsAvailable,
                                supports_artifacts = wrapper.SupportsArtifacts,
                                selected = string.Equals(wrapper.Id, chat.SelectedAgent?.Id, StringComparison.OrdinalIgnoreCase)
                            })
                        },
                        model_dropdown = new
                        {
                            element_id = ChatViewModel.ModelDropdownElementId,
                            expanded = chat.IsModelDropdownOpen,
                            highlighted = chat.IsModelDropdownHighlighted,
                            selected_instance_id = chat.SelectedModel?.InstanceId ?? string.Empty,
                            selected_model_id = chat.SelectedModel?.ModelId ?? string.Empty,
                            selected_label = chat.SelectedModel?.DisplayName ?? "Automatic (best available model)",
                            selected_backend = chat.SelectedModel?.RuntimeBackend ?? "automatic",
                            selected_is_runnable = chat.SelectedModel?.IsRunnable ?? true,
                            options = chat.AvailableModels.Select(option => new
                            {
                                item_id = option.InstanceId,
                                model_id = option.ModelId,
                                element_id = BuildOptionElementId(option.InstanceId),
                                label = option.DisplayName,
                                format = option.Format,
                                runtime_backend = option.RuntimeBackend,
                                is_runnable = option.IsRunnable,
                                runnable_reason = option.RunnableReason,
                                selected = string.Equals(option.InstanceId, chat.SelectedModel?.InstanceId, StringComparison.OrdinalIgnoreCase)
                            })
                        },
                        input = new
                        {
                            element_id = ChatViewModel.InputElementId,
                            text = chat.InputText,
                            highlighted = chat.IsInputHighlighted
                        },
                        attachment_button = new
                        {
                            element_id = ChatViewModel.AttachButtonElementId,
                            visible = chat.CanAttachArtifacts,
                            expanded = chat.ShowArtifactInputBox,
                            highlighted = chat.IsAttachButtonHighlighted
                        },
                        artifact_inputs = new
                        {
                            element_id = ChatViewModel.ArtifactInputsElementId,
                            visible = chat.CanAttachArtifacts && chat.ShowArtifactInputBox,
                            text = chat.ArtifactInputsText,
                            highlighted = _services.UiAutomation.IsHighlighted(ChatViewModel.ArtifactInputsElementId)
                        },
                        controls = new
                        {
                            token_slider = new
                            {
                                element_id = ChatViewModel.MaxTokensElementId,
                                value = chat.MaxTokens,
                                highlighted = chat.IsMaxTokensHighlighted,
                                owner_tab = "Models",
                                location = "models.loaded_instances",
                                compatibility_alias = true,
                                read_only = true
                            },
                            send_button = new
                            {
                                element_id = ChatViewModel.SendButtonElementId,
                                highlighted = chat.IsSendButtonHighlighted,
                                enabled = !chat.IsGenerating
                            },
                            reload_button = new
                            {
                                element_id = ChatViewModel.ReloadButtonElementId,
                                highlighted = chat.IsReloadButtonHighlighted,
                                visible = true
                            },
                            max_tokens = chat.MaxTokens,
                            effective_token_budget = chat.EffectiveMaxTokensStatusText
                        },
                        workflow = new
                        {
                            selected_workflow_id = chat.SelectedWorkflow?.Id ?? string.Empty,
                            selected_workflow_name = chat.SelectedWorkflow?.Name ?? string.Empty,
                            status_text = chat.SelectedWorkflowStatusText,
                            available_workflows = chat.AvailableWorkflows.Select(workflow => new
                            {
                                workflow_id = workflow.Id,
                                label = workflow.DisplayLabel,
                                description = workflow.Description,
                                source = workflow.SourceLabel,
                                step_count = workflow.StepCount
                            })
                        },
                        queue = new
                        {
                            status_text = chat.QueueStatusText,
                            items = chat.QueueItems.Select(item => new
                            {
                                run_id = item.RunId,
                                status = item.Status,
                                status_text = item.StatusText,
                                title = item.Title,
                                prompt_text = item.PromptText,
                                workflow_id = item.WorkflowId,
                                workflow_name = item.WorkflowName,
                                wrapper_id = item.WrapperId,
                                wrapper_name = item.WrapperName,
                                model_id = item.ModelId,
                                model_label = item.ModelLabel,
                                artifact_summary = item.ArtifactSummary,
                                priority = item.Priority,
                                is_active = item.IsActive,
                                created_at_utc = item.CreatedAtUtc,
                                started_at_utc = item.StartedAtUtc,
                                completed_at_utc = item.CompletedAtUtc,
                                error_text = item.ErrorText
                            })
                        }
                    },
                    workflows = new
                    {
                        selector = new
                        {
                            element_id = WorkflowViewModel.WorkflowSelectorElementId,
                            search_element_id = WorkflowViewModel.SearchElementId,
                            bundle_element_id = WorkflowViewModel.BundleIdElementId,
                            search_text = workflows?.SearchText ?? string.Empty,
                            bundle_id = workflows?.BundleIdText ?? string.Empty,
                            selected_workflow_id = workflows?.SelectedLibraryWorkflow?.Id ?? string.Empty,
                            options = workflows?.WorkflowLibrary.Select(item => (object)new
                            {
                                workflow_id = item.Id,
                                label = item.Name,
                                source = item.Source,
                                step_count = item.StepCount
                            }) ?? Enumerable.Empty<object>()
                        },
                        current_workflow = workflows?.CurrentWorkflow is null
                            ? null
                            : new
                            {
                                id = workflows.CurrentWorkflow.Id,
                                name = workflows.CurrentWorkflow.Name,
                                description = workflows.CurrentWorkflow.Description,
                                working_directory = workflows.CurrentWorkflow.WorkingDirectory,
                                source = workflows.CurrentWorkflow.Source,
                                source_path = workflows.CurrentWorkflow.SourcePath,
                                run_status = workflows.CurrentWorkflow.RunStatus,
                                active_run_id = workflows.CurrentWorkflow.ActiveRunId,
                                last_error = workflows.CurrentWorkflow.LastError,
                                global_settings = new
                                {
                                    default_wrapper_id = workflows.CurrentWorkflow.DefaultWrapperId,
                                    default_model_id = workflows.CurrentWorkflow.DefaultModelId,
                                    default_max_tokens = workflows.CurrentWorkflow.DefaultMaxTokensText,
                                    default_timeout_sec = workflows.CurrentWorkflow.DefaultTimeoutSecText,
                                    global_flags = workflows.CurrentWorkflow.GlobalFlags,
                                    environment = workflows.CurrentWorkflow.EnvironmentText,
                                    stop_on_error = workflows.CurrentWorkflow.StopOnError
                                },
                                inputs = workflows.CurrentWorkflow.Inputs.Select(input => new
                                {
                                    key = input.Key,
                                    label = input.Label,
                                    kind = input.Kind,
                                    required = input.Required,
                                    default_value = input.DefaultValue,
                                    current_value = input.CurrentValue
                                }),
                                steps = workflows.CurrentWorkflow.Steps.Select(step => new
                                {
                                    element_id = WorkflowViewModel.BuildStepElementId(step.Id),
                                    run_element_id = WorkflowViewModel.BuildStepRunElementId(step.Id),
                                    id = step.Id,
                                    name = step.Name,
                                    type = step.Type,
                                    enabled = step.Enabled,
                                    expanded = step.IsExpanded,
                                    continue_on_error = step.ContinueOnError,
                                    working_directory_override = step.WorkingDirectoryOverride,
                                    template_id = step.TemplateId,
                                    wrapper_id_override = step.WrapperIdOverride,
                                    model_id_override = step.ModelIdOverride,
                                    max_tokens_override = step.MaxTokensOverrideText,
                                    timeout_sec_override = step.TimeoutSecOverrideText,
                                    raw_flags = step.RawFlags,
                                    prompt_template = step.PromptTemplate,
                                    artifact_inputs = step.ArtifactInputsText,
                                    append_previous_step_output = step.AppendPreviousStepOutput,
                                    integration_id = step.IntegrationId,
                                    tool_name = step.ToolName,
                                    status = step.Status,
                                    status_message = step.StatusMessage,
                                    child_run_id = step.ChildRunId,
                                    final_text = step.FinalText,
                                    response_text = step.ResponseText,
                                    error = step.Error,
                                    logs = step.LogsText,
                                    arguments = step.Arguments.Select(argument => new
                                    {
                                        key = argument.Key,
                                        value = argument.Value
                                    }),
                                    input_bindings = step.InputBindings.Select(binding => new
                                    {
                                        target = binding.Target,
                                        source_type = binding.SourceType,
                                        source_key = binding.SourceKey,
                                        literal_value = binding.LiteralValue
                                    }),
                                    output_bindings = step.OutputBindings.Select(binding => new
                                    {
                                        source = binding.Source,
                                        target_label = binding.TargetLabel
                                    })
                                })
                            }
                    }
                }
            });
        });
    }

    private async Task<object> HandleSetChatTokensAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var value = body.TryGetProperty("value", out var valueElement) && valueElement.TryGetInt32(out var parsed)
            ? parsed
            : 0;

        if (value <= 0)
        {
            return CreateErrorResponse("set_chat_tokens", "INVALID_INPUT", "Missing or invalid 'value' parameter.");
        }

        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            if (chat is null)
            {
                return CreateErrorResponse("set_chat_tokens", "CHAT_NOT_READY", "Chat view model is not available.");
            }

            return CreateErrorResponse("set_chat_tokens", "MOVED_TO_MODELS", "Token budgets are configured in Models.");
        });
    }

    private async Task<object> HandleClickElementAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var elementId = body.TryGetProperty("element_id", out var elementElement) ? elementElement.GetString() : null;

        if (string.IsNullOrWhiteSpace(elementId))
        {
            return CreateErrorResponse("click_element", "INVALID_INPUT", "Missing 'element_id' parameter.");
        }

        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            var workflows = _services.WorkflowViewModel;
            if (chat is null)
            {
                return CreateErrorResponse("click_element", "CHAT_NOT_READY", "Chat view model is not available.");
            }

            if (((string.Equals(_services.AppState.CurrentTab, "Workflows", StringComparison.OrdinalIgnoreCase)) || (string.Equals(_services.AppState.CurrentTab, "Templates", StringComparison.OrdinalIgnoreCase))))
            {
                if (workflows?.CurrentWorkflow is null)
                {
                    return CreateErrorResponse("click_element", "WORKFLOWS_NOT_READY", "Workflow view model is not available.");
                }

                if (elementId.StartsWith("workflows.step.", StringComparison.OrdinalIgnoreCase))
                {
                    var step = workflows.CurrentWorkflow.Steps
                        .FirstOrDefault(candidate => string.Equals(WorkflowViewModel.BuildStepElementId(candidate.Id), elementId, StringComparison.OrdinalIgnoreCase));
                    if (step is null)
                    {
                        return CreateErrorResponse("click_element", "ELEMENT_NOT_VISIBLE", $"Workflow step element '{elementId}' was not found.");
                    }

                    step.IsExpanded = !step.IsExpanded;
                    return CreateSuccessResponse("click_element", new
                    {
                        element_id = elementId,
                        expanded = step.IsExpanded,
                        workflow_id = workflows.CurrentWorkflow.Id
                    });
                }

                if (elementId.StartsWith("workflows.step_run.", StringComparison.OrdinalIgnoreCase))
                {
                    var step = workflows.CurrentWorkflow.Steps
                        .FirstOrDefault(candidate => string.Equals(WorkflowViewModel.BuildStepRunElementId(candidate.Id), elementId, StringComparison.OrdinalIgnoreCase));
                    if (step is null)
                    {
                        return CreateErrorResponse("click_element", "ELEMENT_NOT_VISIBLE", $"Workflow step run element '{elementId}' was not found.");
                    }

                    step.RunStepCommand.Execute(null);
                    return CreateSuccessResponse("click_element", new
                    {
                        element_id = elementId,
                        workflow_id = workflows.CurrentWorkflow.Id,
                        step_id = step.Id
                    });
                }

                switch (elementId)
                {
                    case var id when string.Equals(id, WorkflowViewModel.RunWorkflowElementId, StringComparison.OrdinalIgnoreCase):
                        workflows.RunWorkflowCommand.Execute(null);
                        break;
                    case var id when string.Equals(id, WorkflowViewModel.StopWorkflowElementId, StringComparison.OrdinalIgnoreCase):
                        workflows.StopWorkflowCommand.Execute(null);
                        break;
                    case var id when string.Equals(id, WorkflowViewModel.SaveWorkflowElementId, StringComparison.OrdinalIgnoreCase):
                        workflows.SaveWorkflowCommand.Execute(null);
                        break;
                    case var id when string.Equals(id, WorkflowViewModel.SaveBundleElementId, StringComparison.OrdinalIgnoreCase):
                        workflows.SaveBundleCommand.Execute(null);
                        break;
                    case var id when string.Equals(id, WorkflowViewModel.LoadBundleElementId, StringComparison.OrdinalIgnoreCase):
                        workflows.LoadBundleCommand.Execute(null);
                        break;
                    case var id when string.Equals(id, WorkflowViewModel.AddInputElementId, StringComparison.OrdinalIgnoreCase):
                        workflows.AddInputLabelCommand.Execute(null);
                        break;
                    case var id when string.Equals(id, WorkflowViewModel.AddAgentStepElementId, StringComparison.OrdinalIgnoreCase):
                        workflows.AddAgentStepCommand.Execute(null);
                        break;
                    case var id when string.Equals(id, WorkflowViewModel.AddToolStepElementId, StringComparison.OrdinalIgnoreCase):
                        workflows.AddToolStepCommand.Execute(null);
                        break;
                    default:
                        return CreateErrorResponse("click_element", "UNSUPPORTED_ELEMENT", $"Element '{elementId}' is not supported.");
                }

                return CreateSuccessResponse("click_element", new
                {
                    element_id = elementId,
                    workflow_id = workflows.CurrentWorkflow.Id,
                    run_status = workflows.CurrentWorkflow.RunStatus,
                    active_run_id = workflows.CurrentWorkflow.ActiveRunId
                });
            }

            if (!IsChatTabName(_services.AppState.CurrentTab))
            {
                return CreateErrorResponse("click_element", "ELEMENT_NOT_VISIBLE", "The Chat tab is not active.");
            }

            if (!chat.ToggleElement(elementId, out var error))
            {
                return CreateErrorResponse("click_element", "UNSUPPORTED_ELEMENT", error);
            }

            return CreateSuccessResponse("click_element", new
            {
                element_id = elementId,
                highlighted = _services.UiAutomation.IsHighlighted(elementId!),
                expanded = _services.UiAutomation.IsExpanded(elementId!)
            });
        });
    }

    private async Task<object> HandleSelectListItemAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var elementId = body.TryGetProperty("element_id", out var elementElement) ? elementElement.GetString() : null;
        var itemId = body.TryGetProperty("item_id", out var itemElement) ? itemElement.GetString() : null;
        var label = body.TryGetProperty("label", out var labelElement) ? labelElement.GetString() : null;

        if (string.IsNullOrWhiteSpace(elementId))
        {
            return CreateErrorResponse("select_list_item", "INVALID_INPUT", "Missing 'element_id' parameter.");
        }

        if (string.IsNullOrWhiteSpace(itemId) && string.IsNullOrWhiteSpace(label))
        {
            return CreateErrorResponse("select_list_item", "INVALID_INPUT", "Either 'item_id' or 'label' is required.");
        }

        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            var workflows = _services.WorkflowViewModel;
            if (chat is null)
            {
                return CreateErrorResponse("select_list_item", "CHAT_NOT_READY", "Chat view model is not available.");
            }

            if (((string.Equals(_services.AppState.CurrentTab, "Workflows", StringComparison.OrdinalIgnoreCase)) || (string.Equals(_services.AppState.CurrentTab, "Templates", StringComparison.OrdinalIgnoreCase))))
            {
                if (workflows is null)
                {
                    return CreateErrorResponse("select_list_item", "WORKFLOWS_NOT_READY", "Workflow view model is not available.");
                }

                if (!string.Equals(elementId, WorkflowViewModel.WorkflowSelectorElementId, StringComparison.OrdinalIgnoreCase))
                {
                    return CreateErrorResponse("select_list_item", "UNSUPPORTED_ELEMENT", $"Element '{elementId}' is not supported.");
                }

                var workflow = workflows.WorkflowLibrary.FirstOrDefault(item =>
                    (!string.IsNullOrWhiteSpace(itemId) && string.Equals(item.Id, itemId, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(label) && (
                        string.Equals(item.Name, label, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(item.DisplayLabel, label, StringComparison.OrdinalIgnoreCase))));

                if (workflow is null)
                {
                    return CreateErrorResponse("select_list_item", "ITEM_NOT_FOUND", "Workflow selection target was not found.");
                }

                workflows.SelectedLibraryWorkflow = workflow;
                return CreateSuccessResponse("select_list_item", new
                {
                    element_id = elementId,
                    selected_item_id = workflow.Id,
                    selected_label = workflow.Name
                });
            }

            if (!IsChatTabName(_services.AppState.CurrentTab))
            {
                return CreateErrorResponse("select_list_item", "ELEMENT_NOT_VISIBLE", "The Chat tab is not active.");
            }

            return elementId switch
            {
                var id when string.Equals(id, ChatViewModel.ModelDropdownElementId, StringComparison.OrdinalIgnoreCase) =>
                    chat.TrySelectModelOption(itemId, label, out var modelError)
                        ? CreateSuccessResponse("select_list_item", new
                        {
                            element_id = elementId,
                            selected_item_id = chat.SelectedModel?.InstanceId ?? string.Empty,
                            selected_model_id = chat.SelectedModel?.ModelId ?? string.Empty,
                            selected_label = chat.SelectedModel?.DisplayName ?? string.Empty,
                            highlighted = _services.UiAutomation.IsHighlighted(elementId!),
                            expanded = _services.UiAutomation.IsExpanded(elementId!)
                        })
                        : CreateErrorResponse("select_list_item", "ITEM_NOT_FOUND", modelError),
                var id when string.Equals(id, ChatViewModel.WrapperDropdownElementId, StringComparison.OrdinalIgnoreCase) =>
                    chat.TrySelectWrapperOption(itemId, label, out var wrapperError)
                        ? CreateSuccessResponse("select_list_item", new
                        {
                            element_id = elementId,
                            selected_item_id = chat.SelectedWrapper?.Id ?? string.Empty,
                            selected_label = chat.SelectedWrapper?.Name ?? string.Empty,
                            highlighted = _services.UiAutomation.IsHighlighted(elementId!),
                            expanded = _services.UiAutomation.IsExpanded(elementId!)
                        })
                        : CreateErrorResponse("select_list_item", "ITEM_NOT_FOUND", wrapperError),
                _ => CreateErrorResponse("select_list_item", "UNSUPPORTED_ELEMENT", $"Element '{elementId}' is not supported.")
            };
        });
    }

    private async Task<object> HandleGetChatStateAsync()
    {
        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            if (chat is null)
            {
                return CreateErrorResponse("get_chat_state", "CHAT_NOT_READY", "Chat view model is not available.");
            }

            var snapshot = chat.GetSnapshot();
            return CreateSuccessResponse("get_chat_state", new
            {
                execution_mode = snapshot.ExecutionMode,
                template_id = snapshot.TemplateId,
                template_name = snapshot.TemplateName,
                wrapper_id = snapshot.WrapperId,
                wrapper_name = snapshot.WrapperName,
                supported_input_kinds = snapshot.SupportedInputKinds,
                artifact_inputs_text = snapshot.ArtifactInputsText,
                session_id = snapshot.SessionId,
                selected_model_id = snapshot.SelectedModelId,
                selected_instance_id = snapshot.SelectedInstanceId,
                dropdown_selected_model_id = snapshot.SelectedModelId,
                dropdown_selected_instance_id = snapshot.SelectedInstanceId,
                selected_model_label = snapshot.SelectedModelLabel,
                selected_backend = snapshot.SelectedBackend,
                selected_is_runnable = snapshot.SelectedIsRunnable,
                resolved_model_id = snapshot.ResolvedModelId,
                resolved_instance_id = snapshot.ResolvedInstanceId,
                resolved_execution_mode = snapshot.ResolvedExecutionMode,
                output_quality_state = snapshot.OutputQualityState,
                latest_run_id = snapshot.LatestRunId,
                input_text = snapshot.InputText
            });
        });
    }

    private async Task<object> HandleGetLabBootstrapAsync()
    {
        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            if (chat is null)
            {
                return CreateErrorResponse("get_lab_bootstrap", "CHAT_NOT_READY", "Lab view model is not available.");
            }

            return CreateSuccessResponse("get_lab_bootstrap", BuildLabStatePayload(chat, includeOptions: true));
        });
    }

    private async Task<object> HandleGetLabStateAsync()
    {
        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            if (chat is null)
            {
                return CreateErrorResponse("get_lab_state", "CHAT_NOT_READY", "Lab view model is not available.");
            }

            return CreateSuccessResponse("get_lab_state", BuildLabStatePayload(chat, includeOptions: false));
        });
    }

    private async Task<object> HandleListWorkspacesAsync()
    {
        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            if (chat is null)
            {
                return CreateErrorResponse("list_workspaces", "CHAT_NOT_READY", "Lab view model is not available.");
            }

            return CreateSuccessResponse("list_workspaces", new
            {
                active_workspace_id = chat.SelectedWorkspace?.Id ?? string.Empty,
                workspaces = chat.OpenWorkspaces.Select(BuildWorkspaceSummaryPayload)
            });
        });
    }

    private async Task<object> HandleSelectWorkspaceAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var workspaceId = body.TryGetProperty("workspace_id", out var workspaceElement) ? workspaceElement.GetString() : null;

        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            if (chat is null)
            {
                return CreateErrorResponse("select_workspace", "CHAT_NOT_READY", "Lab view model is not available.");
            }

            return chat.TrySelectWorkspace(workspaceId, out var error)
                ? CreateSuccessResponse("select_workspace", new
                {
                    workspace_id = chat.SelectedWorkspace?.Id ?? string.Empty,
                    workspace_name = chat.SelectedWorkspace?.Name ?? string.Empty,
                    root_directory = chat.ActiveWorkspaceRootDirectory
                })
                : CreateErrorResponse("select_workspace", "WORKSPACE_NOT_FOUND", error);
        });
    }

    private async Task<object> HandleOpenWorkspaceAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var path = body.TryGetProperty("path", out var pathElement) ? pathElement.GetString() : null;

        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            if (chat is null)
            {
                return CreateErrorResponse("open_workspace", "CHAT_NOT_READY", "Lab view model is not available.");
            }

            return chat.TryOpenWorkspace(path, out var error)
                ? CreateSuccessResponse("open_workspace", new
                {
                    workspace_id = chat.SelectedWorkspace?.Id ?? string.Empty,
                    workspace_name = chat.SelectedWorkspace?.Name ?? string.Empty,
                    root_directory = chat.ActiveWorkspaceRootDirectory
                })
                : CreateErrorResponse("open_workspace", "OPEN_FAILED", error);
        });
    }

    private object HandleGetWorkspaceTree(HttpListenerRequest request)
    {
        var workspaceId = request.QueryString["workspace_id"] ?? string.Empty;
        var directoryPath = request.QueryString["directory_path"] ?? string.Empty;
        var nodes = string.IsNullOrWhiteSpace(directoryPath)
            ? _services.Workspaces.GetRootNodes(workspaceId)
            : _services.Workspaces.GetChildren(workspaceId, directoryPath);

        return CreateSuccessResponse("get_workspace_tree", new
        {
            workspace_id = workspaceId,
            directory_path = directoryPath,
            nodes = nodes.Select(BuildWorkspaceNodePayload)
        });
    }

    private object HandleGetWorkspaceFile(HttpListenerRequest request)
    {
        var workspaceId = request.QueryString["workspace_id"] ?? string.Empty;
        var path = request.QueryString["path"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(workspaceId) || string.IsNullOrWhiteSpace(path))
        {
            return CreateErrorResponse("get_workspace_file", "INVALID_INPUT", "Missing workspace_id or path.");
        }

        try
        {
            var content = _services.Workspaces.ReadFileText(workspaceId, path);
            var relativePath = _services.Workspaces.ResolveWorkspaceRelativePath(workspaceId, path) ?? string.Empty;
            return CreateSuccessResponse("get_workspace_file", new
            {
                workspace_id = workspaceId,
                path,
                relative_path = relativePath,
                content,
                editable = _services.Workspaces.IsEditableWorkspace(workspaceId)
            });
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("get_workspace_file", "FILE_READ_FAILED", ex.Message);
        }
    }

    private async Task<object> HandleSelectWorkspaceNodeAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var workspaceId = body.TryGetProperty("workspace_id", out var workspaceElement) ? workspaceElement.GetString() : null;
        var path = body.TryGetProperty("path", out var pathElement) ? pathElement.GetString() : null;

        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            if (chat is null)
            {
                return CreateErrorResponse("select_workspace_node", "CHAT_NOT_READY", "Lab view model is not available.");
            }

            return chat.TrySelectWorkspaceNode(workspaceId, path, out var error)
                ? CreateSuccessResponse("select_workspace_node", new
                {
                    workspace_id = chat.SelectedWorkspace?.Id ?? string.Empty,
                    selected_path = chat.SelectedWorkspaceNode?.FullPath ?? string.Empty
                })
                : CreateErrorResponse("select_workspace_node", "PATH_NOT_FOUND", error);
        });
    }

    private async Task<object> HandleSaveSandboxFileAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var workspaceId = body.TryGetProperty("workspace_id", out var workspaceElement) ? workspaceElement.GetString() : WorkspaceService.SandboxWorkspaceId;
        var path = body.TryGetProperty("path", out var pathElement) ? pathElement.GetString() : null;
        var text = body.TryGetProperty("text", out var textElement) ? textElement.GetString() : string.Empty;
        if (!_services.Workspaces.IsEditableWorkspace(workspaceId))
        {
            return CreateErrorResponse("save_sandbox_file", "WORKSPACE_READ_ONLY", "Only the sandbox workspace is editable from Nexus Web Lab.");
        }

        try
        {
            _services.Workspaces.SaveFileText(workspaceId, path, text);
            return await RunOnUiThreadAsync(() =>
            {
                var chat = _services.Chat;
                chat?.TrySelectWorkspaceNode(workspaceId, path, out _);
                return CreateSuccessResponse("save_sandbox_file", new
                {
                    workspace_id = workspaceId,
                    path
                });
            });
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("save_sandbox_file", "SAVE_FAILED", ex.Message);
        }
    }

    private async Task<object> HandleDeleteSandboxPathAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var workspaceId = body.TryGetProperty("workspace_id", out var workspaceElement) ? workspaceElement.GetString() : WorkspaceService.SandboxWorkspaceId;
        var path = body.TryGetProperty("path", out var pathElement) ? pathElement.GetString() : null;
        if (!_services.Workspaces.IsEditableWorkspace(workspaceId))
        {
            return CreateErrorResponse("delete_sandbox_path", "WORKSPACE_READ_ONLY", "Only the sandbox workspace is editable from Nexus Web Lab.");
        }

        try
        {
            _services.Workspaces.DeletePath(workspaceId, path);
            return await RunOnUiThreadAsync(() =>
            {
                var chat = _services.Chat;
                if (chat is not null && chat.SelectedWorkspaceNode is not null &&
                    string.Equals(chat.SelectedWorkspaceNode.FullPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    chat.TrySelectWorkspace(workspaceId, out _);
                }

                return CreateSuccessResponse("delete_sandbox_path", new
                {
                    workspace_id = workspaceId,
                    path
                });
            });
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("delete_sandbox_path", "DELETE_FAILED", ex.Message);
        }
    }

    private async Task<object> HandleCreateSandboxAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var name = body.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;

        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            if (chat is null)
            {
                return CreateErrorResponse("create_sandbox", "CHAT_NOT_READY", "Lab view model is not available.");
            }

            return chat.TryCreateSandbox(name, out var error)
                ? CreateSuccessResponse("create_sandbox", new
                {
                    workspace_id = chat.SelectedWorkspace?.Id ?? string.Empty,
                    workspace_name = chat.SelectedWorkspace?.Name ?? string.Empty,
                    root_directory = chat.ActiveWorkspaceRootDirectory
                })
                : CreateErrorResponse("create_sandbox", "CREATE_FAILED", error);
        });
    }

    private async Task<object> HandleDeleteSandboxAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var workspaceId = body.TryGetProperty("workspace_id", out var workspaceElement) ? workspaceElement.GetString() : null;

        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            if (chat is null)
            {
                return CreateErrorResponse("delete_sandbox", "CHAT_NOT_READY", "Lab view model is not available.");
            }

            return chat.TryDeleteSandbox(workspaceId, out var error)
                ? CreateSuccessResponse("delete_sandbox", new
                {
                    workspace_id = workspaceId ?? string.Empty,
                    active_workspace_id = chat.SelectedWorkspace?.Id ?? string.Empty,
                    active_workspace_name = chat.SelectedWorkspace?.Name ?? string.Empty
                })
                : CreateErrorResponse("delete_sandbox", "DELETE_FAILED", error);
        });
    }

    private async Task<object> HandleClearSandboxAsync(HttpListenerRequest request)
    {
        var body = request.HasEntityBody ? await ReadBodyAsJsonAsync(request) : default;
        var workspaceId = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("workspace_id", out var workspaceElement)
            ? workspaceElement.GetString()
            : WorkspaceService.SandboxWorkspaceId;
        try
        {
            var before = _services.Workspaces.ClearSandbox(workspaceId);
            return await RunOnUiThreadAsync(() =>
            {
                var chat = _services.Chat;
                chat?.TrySelectWorkspace(workspaceId, out _);
                return CreateSuccessResponse("clear_sandbox", new
                {
                    workspace_id = workspaceId,
                    cleared_entries = before,
                    remaining_entries = _services.Workspaces.GetRootNodes(workspaceId).Select(BuildWorkspaceNodePayload)
                });
            });
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("clear_sandbox", "CLEAR_FAILED", ex.Message);
        }
    }

    private async Task<object> HandleCreateSandboxDirectoryAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var workspaceId = body.TryGetProperty("workspace_id", out var workspaceElement) ? workspaceElement.GetString() : WorkspaceService.SandboxWorkspaceId;
        var path = body.TryGetProperty("path", out var pathElement) ? pathElement.GetString() : null;
        if (!_services.Workspaces.IsEditableWorkspace(workspaceId))
        {
            return CreateErrorResponse("create_sandbox_directory", "WORKSPACE_READ_ONLY", "Only the sandbox workspace is editable from Nexus Web Lab.");
        }

        try
        {
            _services.Workspaces.CreateDirectoryPath(workspaceId, path);
            return CreateSuccessResponse("create_sandbox_directory", new
            {
                workspace_id = workspaceId,
                path
            });
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("create_sandbox_directory", "CREATE_FAILED", ex.Message);
        }
    }

    private async Task<object> HandleRenameSandboxPathAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var workspaceId = body.TryGetProperty("workspace_id", out var workspaceElement) ? workspaceElement.GetString() : WorkspaceService.SandboxWorkspaceId;
        var sourcePath = body.TryGetProperty("source_path", out var sourceElement) ? sourceElement.GetString() : null;
        var destinationPath = body.TryGetProperty("destination_path", out var destinationElement) ? destinationElement.GetString() : null;
        if (!_services.Workspaces.IsEditableWorkspace(workspaceId))
        {
            return CreateErrorResponse("rename_sandbox_path", "WORKSPACE_READ_ONLY", "Only the sandbox workspace is editable from Nexus Web Lab.");
        }

        try
        {
            _services.Workspaces.RenamePath(workspaceId, sourcePath, destinationPath);
            return CreateSuccessResponse("rename_sandbox_path", new
            {
                workspace_id = workspaceId,
                source_path = sourcePath,
                destination_path = destinationPath
            });
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("rename_sandbox_path", "RENAME_FAILED", ex.Message);
        }
    }

    private async Task<object> HandleSaveSandboxToDirectoryAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var targetDirectory = body.TryGetProperty("target_directory", out var targetElement) ? targetElement.GetString() : null;

        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            if (chat is null)
            {
                return CreateErrorResponse("save_sandbox_to_directory", "CHAT_NOT_READY", "Lab view model is not available.");
            }

            return chat.TrySaveSandboxToDirectory(targetDirectory ?? string.Empty, out var error)
                ? CreateSuccessResponse("save_sandbox_to_directory", new
                {
                    target_directory = targetDirectory ?? string.Empty,
                    active_workspace_id = chat.SelectedWorkspace?.Id ?? string.Empty
                })
                : CreateErrorResponse("save_sandbox_to_directory", "EXPORT_FAILED", error);
        });
    }

    private object HandleGetLabSuiteProjects(HttpListenerRequest request)
    {
        var pack = request.QueryString["pack"] ?? "all";
        try
        {
            var projects = LoadLabSuiteProjects(pack);
            return CreateSuccessResponse("get_lab_suite_projects", new
            {
                pack,
                projects
            });
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("get_lab_suite_projects", "LOAD_FAILED", ex.Message);
        }
    }

    private async Task<object> HandleRunLabSuiteProjectAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var slug = body.TryGetProperty("slug", out var slugElement) ? slugElement.GetString() : string.Empty;
        var pack = body.TryGetProperty("pack", out var packElement) ? packElement.GetString() : "all";
        if (string.IsNullOrWhiteSpace(slug))
        {
            return CreateErrorResponse("run_lab_suite_project", "INVALID_INPUT", "Missing 'slug' parameter.");
        }

        LabSuiteProjectPayload? project;
        try
        {
            project = LoadLabSuiteProjects(pack ?? "all").FirstOrDefault(item => string.Equals(item.Slug, slug, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("run_lab_suite_project", "LOAD_FAILED", ex.Message);
        }

        if (project is null)
        {
            return CreateErrorResponse("run_lab_suite_project", "PROJECT_NOT_FOUND", $"Suite project '{slug}' was not found.");
        }

        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            if (chat is null)
            {
                return CreateErrorResponse("run_lab_suite_project", "CHAT_NOT_READY", "Chat view model is not available.");
            }

            Directory.CreateDirectory(project.ProjectDirectory);
            if (!chat.TryOpenWorkspace(project.ProjectDirectory, out var workspaceError))
            {
                return CreateErrorResponse("run_lab_suite_project", "WORKSPACE_FAILED", workspaceError);
            }

            if (!chat.ApplyTemplateById("nexusagent_task", "nexusglmharness", out var templateError))
            {
                return CreateErrorResponse("run_lab_suite_project", "TEMPLATE_FAILED", templateError);
            }

            var prompt = BuildLabSuiteRunPrompt(project);
            if (!chat.TryStartSend(prompt, new List<string>(), out var runId, out var error))
            {
                return CreateErrorResponse("run_lab_suite_project", "SEND_FAILED", error);
            }

            return CreateSuccessResponse("run_lab_suite_project", new
            {
                run_id = runId,
                pack = project.Pack,
                slug = project.Slug,
                project_directory = project.ProjectDirectory,
                prompt_path = project.PromptPath
            });
        });
    }

    private async Task<object> HandleOpenHumanViewPreviewAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var projectPath = body.TryGetProperty("project_path", out var projectElement) ? projectElement.GetString() ?? string.Empty : string.Empty;
        var previewUrl = body.TryGetProperty("preview_url", out var previewElement) ? previewElement.GetString() ?? string.Empty : string.Empty;
        lock (_humanViewGate)
        {
            _humanViewProjectPath = projectPath;
            _humanViewPreviewUrl = previewUrl;
            _humanViewActions.Clear();
            _humanViewConsole.Clear();
            _humanViewActions.Add(new
            {
                action = "open_preview",
                project_path = projectPath,
                preview_url = previewUrl,
                observed = string.IsNullOrWhiteSpace(previewUrl)
                    ? "No preview URL supplied; start the generated app and provide its local URL."
                    : "Preview URL registered for the Interaction Zone.",
                status = string.IsNullOrWhiteSpace(previewUrl) ? "unclear" : "pass",
                timestamp = DateTime.UtcNow
            });
        }

        return CreateSuccessResponse("open_human_view_preview", BuildHumanViewPayload());
    }

    private async Task<object> HandleHumanViewActionAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var action = body.TryGetProperty("action", out var actionElement) ? actionElement.GetString() ?? "observe" : "observe";
        var target = body.TryGetProperty("target", out var targetElement) ? targetElement.GetString() ?? string.Empty : string.Empty;
        var expected = body.TryGetProperty("expected", out var expectedElement) ? expectedElement.GetString() ?? string.Empty : string.Empty;
        lock (_humanViewGate)
        {
            _humanViewActions.Add(new
            {
                action,
                target,
                expected,
                observed = "Action recorded by Web Lab visualization surface; harness executor owns deterministic validation.",
                status = "recorded",
                timestamp = DateTime.UtcNow
            });
        }

        return CreateSuccessResponse("human_view_action", BuildHumanViewPayload());
    }

    private object HandleHumanViewSnapshot()
    {
        return CreateSuccessResponse("human_view_snapshot", BuildHumanViewPayload());
    }

    private object HandleHumanViewConsole()
    {
        lock (_humanViewGate)
        {
            return CreateSuccessResponse("human_view_console", new
            {
                preview_url = _humanViewPreviewUrl,
                entries = _humanViewConsole.ToArray()
            });
        }
    }

    private async Task<object> HandleHumanViewCursorAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var x = body.TryGetProperty("x", out var xElement) && xElement.TryGetInt32(out var parsedX) ? parsedX : 0;
        var y = body.TryGetProperty("y", out var yElement) && yElement.TryGetInt32(out var parsedY) ? parsedY : 0;
        lock (_humanViewGate)
        {
            _humanViewCursorX = Math.Max(0, x);
            _humanViewCursorY = Math.Max(0, y);
        }

        return CreateSuccessResponse("human_view_cursor", BuildHumanViewPayload());
    }

    private object BuildHumanViewPayload()
    {
        lock (_humanViewGate)
        {
            return new
            {
                project_path = _humanViewProjectPath,
                preview_url = _humanViewPreviewUrl,
                cursor = new
                {
                    x = _humanViewCursorX,
                    y = _humanViewCursorY
                },
                actions = _humanViewActions.ToArray(),
                console = _humanViewConsole.ToArray(),
                observed_state = string.IsNullOrWhiteSpace(_humanViewPreviewUrl)
                    ? "No preview URL registered."
                    : "Preview URL registered. Harness artifacts contain authoritative action observations."
            };
        }
    }

    private IReadOnlyList<LabSuiteProjectPayload> LoadLabSuiteProjects(string pack)
    {
        var normalizedPack = string.IsNullOrWhiteSpace(pack) ? "all" : pack.Trim().ToLowerInvariant();
        var suiteFiles = normalizedPack switch
        {
            "a" => new[] { ("a", Path.Combine(_services.Paths.ResolvedRootDirectory, "lab-build-suite", "suite.json")) },
            "b" => new[] { ("b", Path.Combine(_services.Paths.ResolvedRootDirectory, "lab-build-suite", "suite-b.json")) },
            "all" => new[]
            {
                ("a", Path.Combine(_services.Paths.ResolvedRootDirectory, "lab-build-suite", "suite.json")),
                ("b", Path.Combine(_services.Paths.ResolvedRootDirectory, "lab-build-suite", "suite-b.json"))
            },
            _ => throw new InvalidOperationException($"Unknown suite pack '{pack}'.")
        };
        var projects = new List<LabSuiteProjectPayload>();
        foreach (var (suitePack, suitePath) in suiteFiles)
        {
            if (!File.Exists(suitePath))
            {
                continue;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(suitePath));
            var root = document.RootElement;
            var outputRoot = root.TryGetProperty("output_root", out var outputRootElement)
                ? outputRootElement.GetString() ?? "lab-build-suite/projects"
                : "lab-build-suite/projects";
            var absoluteOutputRoot = Path.GetFullPath(Path.Combine(_services.Paths.ResolvedRootDirectory, outputRoot));
            if (!root.TryGetProperty("projects", out var projectsElement) || projectsElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var projectElement in projectsElement.EnumerateArray())
            {
                var slug = projectElement.TryGetProperty("slug", out var slugElement) ? slugElement.GetString() ?? string.Empty : string.Empty;
                var prompt = projectElement.TryGetProperty("prompt", out var promptElement) ? promptElement.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(prompt))
                {
                    continue;
                }

                var promptPath = Path.GetFullPath(Path.Combine(_services.Paths.ResolvedRootDirectory, "lab-build-suite", prompt));
                projects.Add(new LabSuiteProjectPayload
                {
                    Pack = suitePack,
                    Slug = slug,
                    Kind = projectElement.TryGetProperty("kind", out var kindElement) ? kindElement.GetString() ?? string.Empty : string.Empty,
                    Stack = projectElement.TryGetProperty("stack", out var stackElement) ? stackElement.GetString() ?? string.Empty : string.Empty,
                    ExpectedTitle = projectElement.TryGetProperty("expected_title", out var titleElement) ? titleElement.GetString() ?? string.Empty : string.Empty,
                    LongFileMinLines = projectElement.TryGetProperty("long_file_min_lines", out var linesElement) && linesElement.TryGetInt32(out var lines) ? lines : 0,
                    PromptPath = promptPath,
                    ProjectDirectory = Path.Combine(absoluteOutputRoot, slug)
                });
            }
        }

        return projects;
    }

    private static string BuildLabSuiteRunPrompt(LabSuiteProjectPayload project)
    {
        var prompt = File.Exists(project.PromptPath) ? File.ReadAllText(project.PromptPath).Trim() : string.Empty;
        return $"""
{prompt}

# NexusCentral Web Lab suite run contract

Build the complete project directly in this selected workspace root:
{project.ProjectDirectory}

Do not summarize instead of writing files. Produce a complete multi-file project that satisfies the prompt. Required output files:
- package.json
- index.html
- src/
- README.md
- validation.md
- harness-report.json

Expected title: {project.ExpectedTitle}
Project kind: {project.Kind}
Stack: {project.Stack}
Long-file minimum line count: {project.LongFileMinLines}

If a long-file minimum is greater than 0, validation.md must name the file and exact line count.
Keep everything local-only: no backend, remote APIs, remote assets, CDN dependencies, credentials, payments, or deployment.
The project must pass npm install and npm run build.
""";
    }

    private async Task<object> HandleSelectLabAgentAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var agentId = body.TryGetProperty("agent_id", out var agentElement) ? agentElement.GetString() : null;
        var label = body.TryGetProperty("label", out var labelElement) ? labelElement.GetString() : null;

        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            if (chat is null)
            {
                return CreateErrorResponse("select_lab_agent", "CHAT_NOT_READY", "Lab view model is not available.");
            }

            return chat.TrySelectAgentOption(agentId, label, out var error)
                ? CreateSuccessResponse("select_lab_agent", new
                {
                    agent_id = chat.SelectedAgent?.Id ?? string.Empty,
                    agent_name = chat.SelectedAgent?.Name ?? string.Empty
                })
                : CreateErrorResponse("select_lab_agent", "AGENT_NOT_FOUND", error);
        });
    }

    private async Task<object> HandleSelectLabModelAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var modelId = body.TryGetProperty("model_id", out var modelElement) ? modelElement.GetString() : null;
        var label = body.TryGetProperty("label", out var labelElement) ? labelElement.GetString() : null;

        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            if (chat is null)
            {
                return CreateErrorResponse("select_lab_model", "CHAT_NOT_READY", "Lab view model is not available.");
            }

            return chat.TrySelectModelOption(modelId, label, out var error)
                ? CreateSuccessResponse("select_lab_model", new
                {
                    model_id = chat.SelectedModel?.ModelId ?? string.Empty,
                    instance_id = chat.SelectedModel?.InstanceId ?? string.Empty,
                    model_label = chat.SelectedModel?.DisplayName ?? string.Empty
                })
                : CreateErrorResponse("select_lab_model", "MODEL_NOT_FOUND", error);
        });
    }

    private async Task<object> HandleSetLabAgentSettingAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var settingId = body.TryGetProperty("setting_id", out var settingElement) ? settingElement.GetString() : null;
        var value = body.TryGetProperty("value", out var valueElement) ? valueElement.ToString() : string.Empty;

        if (string.IsNullOrWhiteSpace(settingId))
        {
            return CreateErrorResponse("set_lab_agent_setting", "INVALID_INPUT", "Missing 'setting_id' parameter.");
        }

        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            if (chat is null)
            {
                return CreateErrorResponse("set_lab_agent_setting", "CHAT_NOT_READY", "Lab view model is not available.");
            }

            return chat.TrySetExposedAgentSetting(settingId, value, out var error)
                ? CreateSuccessResponse("set_lab_agent_setting", new
                {
                    setting_id = settingId,
                    value
                })
                : CreateErrorResponse("set_lab_agent_setting", "SETTING_NOT_FOUND", error);
        });
    }

    private async Task<object> HandleSetTextAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var elementId = body.TryGetProperty("element_id", out var elementElement) ? elementElement.GetString() : ChatViewModel.InputElementId;
        var text = body.TryGetProperty("text", out var textElement) ? textElement.GetString() : string.Empty;

        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            var workflows = _services.WorkflowViewModel;
            if (chat is null)
            {
                return CreateErrorResponse("set_text", "CHAT_NOT_READY", "Chat view model is not available.");
            }

            if (((string.Equals(_services.AppState.CurrentTab, "Workflows", StringComparison.OrdinalIgnoreCase)) || (string.Equals(_services.AppState.CurrentTab, "Templates", StringComparison.OrdinalIgnoreCase))))
            {
                if (workflows is null)
                {
                    return CreateErrorResponse("set_text", "WORKFLOWS_NOT_READY", "Workflow view model is not available.");
                }

                if (string.Equals(elementId, WorkflowViewModel.SearchElementId, StringComparison.OrdinalIgnoreCase))
                {
                    workflows.SearchText = text ?? string.Empty;
                    return CreateSuccessResponse("set_text", new
                    {
                        element_id = elementId,
                        text = workflows.SearchText
                    });
                }

                if (string.Equals(elementId, WorkflowViewModel.BundleIdElementId, StringComparison.OrdinalIgnoreCase))
                {
                    workflows.BundleIdText = text ?? string.Empty;
                    return CreateSuccessResponse("set_text", new
                    {
                        element_id = elementId,
                        text = workflows.BundleIdText
                    });
                }

                return CreateErrorResponse("set_text", "UNSUPPORTED_ELEMENT", $"Element '{elementId}' is not supported.");
            }

            if (string.Equals(elementId, ChatViewModel.InputElementId, StringComparison.OrdinalIgnoreCase))
            {
                chat.SetInputTextFromTool(text ?? string.Empty);
                ChatView.SyncSemanticInputText(chat.InputText);
                var effectiveText = ChatView.GetSemanticInputText();
                if (!string.IsNullOrEmpty(effectiveText) && !string.Equals(effectiveText, chat.InputText, StringComparison.Ordinal))
                {
                    chat.SetInputTextFromTool(effectiveText);
                }

                return CreateSuccessResponse("set_text", new
                {
                    element_id = elementId,
                    text = chat.InputText
                });
            }

            if (string.Equals(elementId, ChatViewModel.ArtifactInputsElementId, StringComparison.OrdinalIgnoreCase))
            {
                chat.SetArtifactInputsTextFromTool(text ?? string.Empty);
                return CreateSuccessResponse("set_text", new
                {
                    element_id = elementId,
                    text = chat.ArtifactInputsText
                });
            }

            return CreateErrorResponse("set_text", "UNSUPPORTED_ELEMENT", $"Element '{elementId}' is not supported.");
        });
    }

    private async Task<object> HandleTypeTextAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var elementId = body.TryGetProperty("element_id", out var elementElement) ? elementElement.GetString() : ChatViewModel.InputElementId;
        var text = body.TryGetProperty("text", out var textElement) ? textElement.GetString() : string.Empty;

        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            var workflows = _services.WorkflowViewModel;
            if (chat is null)
            {
                return CreateErrorResponse("type_text", "CHAT_NOT_READY", "Chat view model is not available.");
            }

            if (((string.Equals(_services.AppState.CurrentTab, "Workflows", StringComparison.OrdinalIgnoreCase)) || (string.Equals(_services.AppState.CurrentTab, "Templates", StringComparison.OrdinalIgnoreCase))))
            {
                if (workflows is null)
                {
                    return CreateErrorResponse("type_text", "WORKFLOWS_NOT_READY", "Workflow view model is not available.");
                }

                if (string.Equals(elementId, WorkflowViewModel.SearchElementId, StringComparison.OrdinalIgnoreCase))
                {
                    workflows.SearchText += text ?? string.Empty;
                    return CreateSuccessResponse("type_text", new
                    {
                        element_id = elementId,
                        text = workflows.SearchText
                    });
                }

                if (string.Equals(elementId, WorkflowViewModel.BundleIdElementId, StringComparison.OrdinalIgnoreCase))
                {
                    workflows.BundleIdText += text ?? string.Empty;
                    return CreateSuccessResponse("type_text", new
                    {
                        element_id = elementId,
                        text = workflows.BundleIdText
                    });
                }

                return CreateErrorResponse("type_text", "UNSUPPORTED_ELEMENT", $"Element '{elementId}' is not supported.");
            }

            if (string.Equals(elementId, ChatViewModel.InputElementId, StringComparison.OrdinalIgnoreCase))
            {
                chat.TypeInputTextFromTool(text ?? string.Empty);
                ChatView.SyncSemanticInputText(chat.InputText);
                var effectiveText = ChatView.GetSemanticInputText();
                if (!string.IsNullOrEmpty(effectiveText) && !string.Equals(effectiveText, chat.InputText, StringComparison.Ordinal))
                {
                    chat.SetInputTextFromTool(effectiveText);
                }

                return CreateSuccessResponse("type_text", new
                {
                    element_id = elementId,
                    text = chat.InputText
                });
            }

            if (string.Equals(elementId, ChatViewModel.ArtifactInputsElementId, StringComparison.OrdinalIgnoreCase))
            {
                chat.TypeArtifactInputsTextFromTool(text ?? string.Empty);
                return CreateSuccessResponse("type_text", new
                {
                    element_id = elementId,
                    text = chat.ArtifactInputsText
                });
            }

            return CreateErrorResponse("type_text", "UNSUPPORTED_ELEMENT", $"Element '{elementId}' is not supported.");
        });
    }

    private async Task<object> HandleClickButtonAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var elementId = body.TryGetProperty("element_id", out var elementElement) ? elementElement.GetString() : null;

        if (string.IsNullOrWhiteSpace(elementId))
        {
            return CreateErrorResponse("click_button", "INVALID_INPUT", "Missing 'element_id' parameter.");
        }

        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            string runId = string.Empty;
            string error;
            string generationStatus = string.Empty;

            if (chat is null)
            {
                return CreateErrorResponse("click_button", "CHAT_NOT_READY", "Chat view model is not available.");
            }

            var controlInputText = ChatView.GetSemanticInputText();
            if (!string.IsNullOrWhiteSpace(controlInputText) && !string.Equals(controlInputText, chat.InputText, StringComparison.Ordinal))
            {
                chat.SetInputTextFromTool(controlInputText);
            }

            if (!chat.ClickButton(elementId, out var chatRunId, out error))
            {
                return CreateErrorResponse("click_button", "BUTTON_FAILED", error);
            }

            runId = chatRunId ?? string.Empty;
            generationStatus = chat.GenerationStatusText;

            return CreateSuccessResponse("click_button", new
            {
                element_id = elementId,
                run_id = runId,
                generation_status = generationStatus
            });
        });
    }

    private async Task<object> HandleChatSendAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var text = body.TryGetProperty("text", out var textElement) ? textElement.GetString() : null;
        var modelId = body.TryGetProperty("model_id", out var modelElement) ? modelElement.GetString() : null;
        var templateId = body.TryGetProperty("template_id", out var templateElement) ? templateElement.GetString() : null;
        var wrapperId = body.TryGetProperty("wrapper_id", out var wrapperElement) ? wrapperElement.GetString() : null;
        var artifactInputs = body.TryGetProperty("artifact_inputs", out var artifactInputsElement) && artifactInputsElement.ValueKind == JsonValueKind.Array
            ? artifactInputsElement.EnumerateArray().Select(item => item.GetString() ?? string.Empty).Where(item => !string.IsNullOrWhiteSpace(item)).ToList()
            : new List<string>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return CreateErrorResponse("chat_send", "INVALID_INPUT", "Missing 'text' parameter.");
        }

        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            if (chat is null)
            {
                return CreateErrorResponse("chat_send", "CHAT_NOT_READY", "Chat view model is not available.");
            }

            if (!string.IsNullOrWhiteSpace(modelId) && !string.Equals(modelId, chat.SelectedModel?.ModelId, StringComparison.OrdinalIgnoreCase))
            {
                if (!chat.TrySelectModelOption(modelId, null, out var selectionError))
                {
                    return CreateErrorResponse("chat_send", "MODEL_NOT_FOUND", selectionError);
                }
            }

            if (!string.IsNullOrWhiteSpace(templateId) && !chat.ApplyTemplateById(templateId, wrapperId, out var applyError))
            {
                return CreateErrorResponse("chat_send", "TEMPLATE_NOT_FOUND", applyError);
            }

            if (string.IsNullOrWhiteSpace(templateId) && !string.IsNullOrWhiteSpace(wrapperId))
            {
                if (!chat.TrySelectWrapperOption(wrapperId, null, out var wrapperError))
                {
                    return CreateErrorResponse("chat_send", "WRAPPER_NOT_FOUND", wrapperError);
                }
            }

            if (!chat.TryStartSend(text ?? string.Empty, artifactInputs, out var runId, out var error))
            {
                return CreateErrorResponse("chat_send", "SEND_FAILED", error);
            }

            return CreateSuccessResponse("chat_send", new
            {
                run_id = runId,
                execution_mode = chat.SelectedExecutionMode,
                template_id = chat.SelectedTemplate?.Id ?? string.Empty,
                wrapper_id = chat.SelectedWrapper?.Id ?? string.Empty,
                selected_instance_id = chat.SelectedModel?.InstanceId ?? string.Empty,
                selected_model_id = chat.SelectedModel?.ModelId ?? string.Empty,
                max_tokens = chat.MaxTokens
            });
        });
    }

    private async Task<object> HandleGetRunStatusAsync(HttpListenerRequest request)
    {
        var runId = request.QueryString["run_id"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(runId))
        {
            return CreateErrorResponse("get_run_status", "INVALID_INPUT", "Missing 'run_id' query parameter.");
        }

        return await RunOnUiThreadAsync(() =>
        {
            var chat = _services.Chat;
            if (chat is null)
            {
                return CreateErrorResponse("get_run_status", "CHAT_NOT_READY", "Chat view model is not available.");
            }

            var run = chat.GetRun(runId);
            if (run is null)
            {
                return CreateErrorResponse("get_run_status", "RUN_NOT_FOUND", $"Run '{runId}' was not found.");
            }

            return CreateSuccessResponse("get_run_status", new
            {
                run_id = run.RunId,
                status = run.Status,
                prompt = run.Prompt,
                execution_mode = run.ExecutionMode,
                template_id = run.TemplateId,
                template_name = run.TemplateName,
                wrapper_id = run.WrapperId,
                wrapper_name = run.WrapperName,
                selected_instance_id = run.SelectedInstanceId,
                selected_model_id = run.SelectedModelId,
                selected_model_label = run.SelectedModelLabel,
                resolved_instance_id = run.ResolvedInstanceId,
                resolved_model_id = run.ResolvedModelId,
                resolved_execution_mode = run.ResolvedExecutionMode,
                output_quality_state = run.OutputQualityState,
                retry_count = run.RetryCount,
                model_id = run.ModelId,
                model_label = run.ModelLabel,
                backend = run.Backend,
                max_tokens = run.MaxTokens,
                message_count = run.TranscriptMessageCount,
                created_at_utc = run.CreatedAtUtc,
                started_at_utc = run.StartedAtUtc,
                completed_at_utc = run.CompletedAtUtc,
                tokens_generated = run.TokensGenerated,
                elapsed_ms = run.ElapsedMs,
                finish_reason = run.FinishReason,
                output_preview = run.OutputPreview,
                error = run.Error,
                stream_phase = run.StreamPhase,
                is_streaming = run.IsStreaming,
                live_thinking_text = run.LiveThinkingText,
                live_answer_text = run.LiveAnswerText,
                last_stream_update_utc = run.LastStreamUpdateUtc,
                artifact_inputs = run.ArtifactInputs,
                artifact_outputs = run.ArtifactOutputs
            });
        });
    }

    private object BuildLabStatePayload(ChatViewModel chat, bool includeOptions)
    {
        var selectedWorkspace = chat.SelectedWorkspace;
        var selectedNode = chat.SelectedWorkspaceNode;
        var selectedFileContent = string.Empty;
        var selectedFileReadError = string.Empty;
        if (selectedWorkspace is not null && selectedNode is not null && !selectedNode.IsDirectory)
        {
            try
            {
                selectedFileContent = _services.Workspaces.ReadFileText(selectedWorkspace.Id, selectedNode.FullPath);
            }
            catch (Exception ex)
            {
                selectedFileReadError = ex.Message;
            }
        }

        return new
        {
            web_lab = new
            {
                is_running = _services.WebLab.IsRunning,
                local_url = _services.WebLab.LocalUrl,
                advertised_url = _services.WebLab.AdvertisedUrl,
                mobile_url = _services.WebLab.MobileUrl,
                mobile_access_required = true,
                status_text = _services.WebLab.StatusText
            },
            lab = new
            {
                input_text = chat.InputText,
                is_generating = chat.IsGenerating,
                generation_status = chat.GenerationStatusText,
                session_id = chat.SessionId,
                selected_agent_id = chat.SelectedAgent?.Id ?? string.Empty,
                selected_agent_name = chat.SelectedAgent?.Name ?? string.Empty,
                selected_model_id = chat.SelectedModel?.ModelId ?? string.Empty,
                selected_instance_id = chat.SelectedModel?.InstanceId ?? string.Empty,
                selected_model_label = chat.SelectedModel?.DisplayName ?? string.Empty,
                selected_workspace_id = selectedWorkspace?.Id ?? string.Empty,
                selected_workspace_name = selectedWorkspace?.Name ?? string.Empty,
                selected_workspace_root = chat.ActiveWorkspaceRootDirectory,
                selected_node_path = selectedNode?.FullPath ?? string.Empty,
                selected_node_is_directory = selectedNode?.IsDirectory ?? false,
                sandbox_export_path_input = chat.SandboxExportPathInput,
                can_save_sandbox = chat.CanSaveSandbox,
                web_lab_status_text = chat.WebLabStatusText,
                effective_max_tokens = chat.MaxTokens,
                effective_max_tokens_text = chat.EffectiveMaxTokensStatusText,
                shows_agent_selector = chat.ShowAgentSelector
                ,
                exposed_agent_settings = chat.ExposedAgentSettings.Select(setting => new
                {
                    id = setting.Id,
                    label = setting.Label,
                    kind = setting.Kind,
                    value = setting.Value,
                    display_text = setting.DisplayText,
                    is_on = setting.IsOn
                })
            },
            workspaces = chat.OpenWorkspaces.Select(BuildWorkspaceSummaryPayload),
            current_file = selectedNode is null || selectedNode.IsDirectory
                ? null
                : new
                {
                    workspace_id = selectedWorkspace?.Id ?? string.Empty,
                    path = selectedNode.FullPath,
                    relative_path = selectedNode.RelativePath,
                    name = selectedNode.DisplayName,
                    content = selectedFileContent,
                    read_error = selectedFileReadError,
                    editable = selectedWorkspace is not null && _services.Workspaces.IsEditableWorkspace(selectedWorkspace.Id)
                },
            messages = chat.Messages.Select(BuildChatMessagePayload),
            options = includeOptions
                ? new
                {
                    agents = chat.AvailableAgents.Select(agent => new
                    {
                        id = agent.Id,
                        name = agent.Name,
                        description = agent.Description,
                        available = agent.IsAvailable,
                        availability_message = agent.AvailabilityMessage,
                        settings = agent.Settings
                    }),
                    models = chat.AvailableModels.Select(model => new
                    {
                        model_id = model.ModelId,
                        instance_id = model.InstanceId,
                        display_name = model.DisplayName,
                        runtime_backend = model.RuntimeBackend,
                        is_runnable = model.IsRunnable
                    })
                }
                : null
        };
    }

    private static object BuildWorkspaceSummaryPayload(ExplorerTreeNodeViewModel workspace)
    {
        return new
        {
            id = workspace.WorkspaceId,
            name = workspace.WorkspaceName,
            kind = workspace.WorkspaceKind,
            caption = workspace.WorkspaceCaption,
            root_directory = workspace.FullPath,
            path_summary = workspace.PathSummary,
            is_selected = workspace.IsSelected,
            is_expanded = workspace.IsExpanded,
            is_sandbox = workspace.IsSandbox
        };
    }

    private static object BuildWorkspaceNodePayload(WorkspaceFileNode node)
    {
        return new
        {
            name = node.Name,
            full_path = node.FullPath,
            relative_path = node.RelativePath,
            is_directory = node.IsDirectory,
            has_children = node.HasChildren
        };
    }

    private static object BuildChatMessagePayload(ChatMessageViewModel message)
    {
        return new
        {
            role = message.Role,
            text = message.Text,
            status = message.Status,
            is_thinking_phase = message.IsThinkingPhase,
            is_answer_phase = message.IsAnswerPhase,
            is_execution_feed = message.IsExecutionFeed,
            is_feed_expanded = message.IsFeedExpanded,
            live_thinking_text = message.LiveThinkingText,
            live_answer_text = message.IsPlainChatPresentation ? string.Empty : message.LiveAnswerText,
            thinking_text = message.ThinkingText,
            artifacts = message.Artifacts.Select(artifact => new
            {
                label = artifact.Label,
                path = artifact.Path,
                kind = artifact.Kind
            }),
            execution_feed_entries = message.ExecutionFeedEntries.Select(entry => new
            {
                kind = entry.Kind,
                section = entry.Section.ToString().ToLowerInvariant(),
                section_label = entry.SectionLabel,
                text = entry.Text,
                timestamp_utc = entry.Timestamp.UtcDateTime,
                display_timestamp = entry.DisplayTimestamp,
                level = entry.Level,
                is_streaming = entry.IsStreaming,
                show_section_header = entry.ShowSectionHeader,
                artifact_label = entry.ArtifactLabel,
                artifact_path = entry.ArtifactPath,
                validation_status = entry.ValidationStatus,
                approval_id = entry.ApprovalId,
                tool_name = entry.ToolName,
                memory_id = entry.MemoryId
            }),
            execution_feed_chains = message.ExecutionFeedChains.Select(BuildExecutionFeedChainPayload)
        };
    }

    private static object BuildExecutionFeedChainPayload(ChatExecutionFeedChainViewModel chain)
    {
        return new
        {
            chain_id = chain.ChainId,
            parent_chain_id = chain.ParentChainId,
            step_id = chain.StepId,
            phase = chain.Phase,
            title = chain.Title,
            status = chain.Status,
            level = chain.Level,
            depth = chain.Depth,
            is_expanded = chain.IsExpanded,
            is_running = chain.IsRunning,
            streaming_text = chain.StreamingText,
            entries = chain.Entries.Select(entry => new
            {
                kind = entry.Kind,
                section = entry.Section.ToString().ToLowerInvariant(),
                section_label = entry.SectionLabel,
                text = entry.Text,
                timestamp_utc = entry.Timestamp.UtcDateTime,
                display_timestamp = entry.DisplayTimestamp,
                level = entry.Level,
                is_streaming = entry.IsStreaming,
                artifact_label = entry.ArtifactLabel,
                artifact_path = entry.ArtifactPath,
                validation_status = entry.ValidationStatus,
                approval_id = entry.ApprovalId,
                tool_name = entry.ToolName,
                memory_id = entry.MemoryId
            }),
            children = chain.Children.Select(BuildExecutionFeedChainPayload)
        };
    }

    private static object BuildWrapperRunPayload(WrapperRunState state)
    {
        return new
        {
            run_id = state.RunId,
            template_id = state.TemplateId,
            template_name = state.TemplateName,
            wrapper_id = state.WrapperId,
            wrapper_name = state.WrapperName,
            io_contract_id = state.IoContractId,
            status = state.Status,
            status_message = state.StatusMessage,
            session_id = state.SessionId,
            root_directory = state.RootDirectory,
            prompt = state.Prompt,
            text_inputs = state.TextInputs,
            text_outputs = state.TextOutputs,
            primary_text_output = state.PrimaryTextOutput,
            contract_status = state.ContractStatus,
            output_dir = state.OutputDir,
            artifact_inputs = state.ArtifactInputs,
            artifact_outputs = state.ArtifactOutputs.Select(artifact => new
            {
                label = artifact.Label,
                path = artifact.Path,
                kind = artifact.Kind
            }),
            logs = state.Logs,
            final_text = state.FinalText,
            error = state.Error,
            model_id = state.ModelId,
            max_tokens = state.MaxTokens,
            created_at_utc = state.CreatedAtUtc,
            started_at_utc = state.StartedAtUtc,
            completed_at_utc = state.CompletedAtUtc,
            exit_code = state.ExitCode
        };
    }

    private object BuildWorkflowPayload(WorkflowDefinition workflow)
    {
        return new
        {
            id = workflow.Id,
            name = workflow.Name,
            description = workflow.Description,
            working_directory = workflow.WorkingDirectory,
            global_settings = new
            {
                default_wrapper_id = workflow.GlobalSettings.DefaultWrapperId,
                default_model_id = workflow.GlobalSettings.DefaultModelId,
                default_max_tokens = workflow.GlobalSettings.DefaultMaxTokens,
                default_timeout_sec = workflow.GlobalSettings.DefaultTimeoutSec,
                global_flags = workflow.GlobalSettings.GlobalFlags,
                environment = workflow.GlobalSettings.Environment,
                stop_on_error = workflow.GlobalSettings.StopOnError
            },
            inputs = workflow.Inputs.Select(input => new
            {
                key = input.Key,
                label = input.Label,
                kind = input.Kind,
                required = input.Required,
                default_value = input.DefaultValue
            }),
            steps = workflow.Steps.Select(BuildWorkflowStepPayload),
            tags = workflow.Tags,
            source = workflow.Source,
            source_path = workflow.SourcePath
        };
    }

    private static object BuildWorkflowStepPayload(WorkflowStepDefinition step)
    {
        return new
        {
            id = step.Id,
            name = step.Name,
            type = step.Type,
            enabled = step.Enabled,
            working_directory_override = step.WorkingDirectoryOverride,
            continue_on_error = step.ContinueOnError,
            template_id = step.TemplateId,
            wrapper_id_override = step.WrapperIdOverride,
            model_id_override = step.ModelIdOverride,
            max_tokens_override = step.MaxTokensOverride,
            timeout_sec_override = step.TimeoutSecOverride,
            flags = step.Flags.Select(flag => new
            {
                type = flag.Type,
                name = flag.Name,
                value = flag.Value
            }),
            raw_flags = step.RawFlags,
            prompt_template = step.PromptTemplate,
            artifact_inputs = step.ArtifactInputs,
            append_previous_step_output = step.AppendPreviousStepOutput,
            integration_id = step.IntegrationId,
            tool_name = step.ToolName,
            arguments = step.Arguments,
            input_bindings = step.InputBindings.Select(binding => new
            {
                target = binding.Target,
                source_type = binding.SourceType,
                source_key = binding.SourceKey,
                literal_value = binding.LiteralValue
            }),
            output_bindings = step.OutputBindings.Select(binding => new
            {
                source = binding.Source,
                target_label = binding.TargetLabel
            })
        };
    }

    private object BuildWorkflowBundlePayload(WorkflowBundle bundle)
    {
        return new
        {
            format = bundle.Format,
            workflow = BuildWorkflowPayload(bundle.Workflow),
            editor_state = new
            {
                selected_step_id = bundle.EditorState.SelectedStepId,
                expanded_step_ids = bundle.EditorState.ExpandedStepIds
            },
            last_values = bundle.LastValues,
            saved_at_utc = bundle.SavedAtUtc
        };
    }

    private static object BuildWorkflowRunPayload(WorkflowRunState run)
    {
        return new
        {
            run_id = run.RunId,
            workflow_id = run.WorkflowId,
            workflow_name = run.WorkflowName,
            status = run.Status,
            current_step_id = run.CurrentStepId,
            error = run.Error,
            label_values = run.LabelValues,
            created_at_utc = run.CreatedAtUtc,
            started_at_utc = run.StartedAtUtc,
            completed_at_utc = run.CompletedAtUtc,
            steps = run.Steps.Select(BuildWorkflowStepRunPayload)
        };
    }

    private static object BuildWorkflowStepRunPayload(WorkflowStepRunState step)
    {
        return new
        {
            step_id = step.StepId,
            step_name = step.StepName,
            type = step.Type,
            status = step.Status,
            status_message = step.StatusMessage,
            working_directory = step.WorkingDirectory,
            resolved_prompt = step.ResolvedPrompt,
            resolved_tool_call = step.ResolvedToolCall,
            child_run_id = step.ChildRunId,
            final_text = step.FinalText,
            response_text = step.ResponseText,
            error = step.Error,
            logs = step.Logs,
            artifact_outputs = step.ArtifactOutputs.Select(artifact => new
            {
                label = artifact.Label,
                path = artifact.Path,
                kind = artifact.Kind
            }),
            output_labels = step.OutputLabels,
            created_at_utc = step.CreatedAtUtc,
            started_at_utc = step.StartedAtUtc,
            completed_at_utc = step.CompletedAtUtc
        };
    }

    private WorkflowDefinition? ResolveWorkflowFromBody(JsonElement body)
    {
        if (body.TryGetProperty("workflow", out var workflowElement) && workflowElement.ValueKind == JsonValueKind.Object)
        {
            return DeserializeWorkflow(workflowElement);
        }

        var workflowId = body.TryGetProperty("workflow_id", out var workflowIdElement) ? workflowIdElement.GetString() : null;
        return string.IsNullOrWhiteSpace(workflowId)
            ? null
            : _services.Workflows.FindById(workflowId);
    }

    private WorkflowBundle? ResolveWorkflowBundleFromBody(JsonElement body)
    {
        if (body.TryGetProperty("bundle", out var bundleElement) && bundleElement.ValueKind == JsonValueKind.Object)
        {
            return DeserializeWorkflowBundle(bundleElement);
        }

        var workflow = ResolveWorkflowFromBody(body);
        if (workflow is null)
        {
            return null;
        }

        var bundle = new WorkflowBundle
        {
            Workflow = workflow,
            LastValues = body.TryGetProperty("last_values", out var lastValuesElement)
                ? ReadStringDictionary(lastValuesElement)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

        if (body.TryGetProperty("editor_state", out var editorStateElement) && editorStateElement.ValueKind == JsonValueKind.Object)
        {
            bundle.EditorState = JsonSerializer.Deserialize<WorkflowEditorState>(editorStateElement.GetRawText(), WorkflowJsonOptions) ?? new WorkflowEditorState();
        }

        return bundle;
    }

    private static WorkflowDefinition? DeserializeWorkflow(JsonElement workflowElement)
    {
        try
        {
            return JsonSerializer.Deserialize<WorkflowDefinition>(workflowElement.GetRawText(), WorkflowJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static WorkflowBundle? DeserializeWorkflowBundle(JsonElement bundleElement)
    {
        try
        {
            return JsonSerializer.Deserialize<WorkflowBundle>(bundleElement.GetRawText(), WorkflowJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, string> ReadStringDictionary(JsonElement element)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (element.ValueKind != JsonValueKind.Object)
        {
            return values;
        }

        foreach (var property in element.EnumerateObject())
        {
            values[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                JsonValueKind.Null => string.Empty,
                _ => property.Value.GetRawText()
            };
        }

        return values;
    }

    private async Task<bool> ChangeTabAsync(string targetTab)
    {
        var tcs = new TaskCompletionSource<bool>();

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                tcs.SetResult(_services.AppState.TrySelectTab(targetTab));
            }
            catch
            {
                tcs.SetResult(false);
            }
        });

        return await tcs.Task;
    }

    private static async Task<JsonElement> ReadBodyAsJsonAsync(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream);
        var body = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            body = "{}";
        }

        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private object CreateSuccessResponse(string toolName, object data)
    {
        return new
        {
            ok = true,
            integration = "nexuscentral",
            tool = toolName,
            data,
            error = (object?)null
        };
    }

    private object CreateErrorResponse(string toolName, string errorCode, string message)
    {
        return new
        {
            ok = false,
            integration = "nexuscentral",
            tool = toolName,
            data = (object?)null,
            error = new
            {
                code = errorCode,
                message
            }
        };
    }

    private static async Task WriteObjectResponseAsync(HttpListenerResponse response, object payload)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, WorkflowJsonOptions);
        response.ContentType = "application/json";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    private sealed class IntegrationRuntimeStreamProgress : IProgress<RuntimeStreamEvent>
    {
        private readonly StreamWriter _writer;
        private readonly object _gate = new();

        public IntegrationRuntimeStreamProgress(StreamWriter writer)
        {
            _writer = writer;
        }

        public void Report(RuntimeStreamEvent value)
        {
            lock (_gate)
            {
                _writer.WriteLine(JsonSerializer.Serialize(new
                {
                    type = "chunk",
                    phase = value.Phase == RuntimeStreamPhase.Thinking ? "thinking" : "answer",
                    text = value.Text
                }, WorkflowJsonOptions));
                _writer.Flush();
            }
        }
    }

    private sealed class LabSuiteProjectPayload
    {
        public string Pack { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Stack { get; set; } = string.Empty;
        public string ExpectedTitle { get; set; } = string.Empty;
        public int LongFileMinLines { get; set; }
        public string PromptPath { get; set; } = string.Empty;
        public string ProjectDirectory { get; set; } = string.Empty;
    }

    private object CreateNotFoundResponse(string path)
    {
        return CreateErrorResponse(GetToolName(path), "NOT_FOUND", $"Tool route '{path}' was not found.");
    }

    private static string GetToolName(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "unknown";

    private static string BuildOptionElementId(string modelId) =>
        string.IsNullOrWhiteSpace(modelId) ? "chat.model_option.automatic" : $"chat.model_option.{modelId}";

    private static string BuildWrapperOptionElementId(string wrapperId) =>
        $"chat.wrapper_option.{wrapperId}";

    private static string BuildInstanceElementId(string instanceId) =>
        $"router.instance.{instanceId}";

    private static Task<T> RunOnUiThreadAsync<T>(Func<T> action)
    {
        var tcs = new TaskCompletionSource<T>();

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                tcs.SetResult(action());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }
}
