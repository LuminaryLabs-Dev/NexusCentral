using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NexusCentral_Avalonia.Models;
using NexusCentral_Avalonia.Services;
using NexusCentral_Avalonia.Views;

namespace NexusCentral_Avalonia.ViewModels;

public partial class ChatViewModel : ViewModelBase
{
    public const string ModelDropdownElementId = "chat.model_dropdown";
    public const string AgentDropdownElementId = "chat.agent_dropdown";
    public const string AttachButtonElementId = "chat.attach_button";
    public const string InputElementId = "chat.input_box";
    public const string MaxTokensElementId = "chat.max_tokens_slider";
    public const string SendButtonElementId = "chat.send_button";
    public const string ReloadButtonElementId = "chat.reload_button";
    public const string WrapperDropdownElementId = AgentDropdownElementId;
    public const string ArtifactInputsElementId = "chat.artifact_inputs";

    private readonly AppServices _services;
    private readonly Dictionary<string, ChatRunState> _runs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TaskCompletionSource<bool>> _runStartSignals = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _runLock = new();
    private readonly SemaphoreSlim _queuePumpLock = new(1, 1);
    private bool _applyingAgentSelection;
    private bool _applyingAgentDefaults;
    private bool _applyingTemplateDefaults;
    private string _loadedWorkspaceId = string.Empty;
    private bool _loadingWorkspaceSession;
    private bool _persistingWorkspaceSession;
    private bool _queuePumpActive;
    private bool _refreshingWrappers;
    private bool _syncingExplorerSelection;
    private string _selectedWorkspaceId = string.Empty;

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = new();
    public ObservableCollection<ExplorerTreeNodeViewModel> ExplorerRoots { get; } = new();
    public ObservableCollection<ExplorerTreeNodeViewModel> VisibleExplorerNodes { get; } = new();
    public ObservableCollection<ChatModelOptionViewModel> AvailableModels { get; } = new();
    public ObservableCollection<WorkflowLibraryItemViewModel> AvailableWorkflows { get; } = new();
    public ObservableCollection<TemplateDefinition> AvailableTemplates { get; } = new();
    public ObservableCollection<WrapperDefinition> AvailableWrappers { get; } = new();
    public ObservableCollection<WrapperSettingViewModel> ExposedAgentSettings { get; } = new();
    public ObservableCollection<ChatQueueItemViewModel> QueueItems { get; } = new();
    public IEnumerable<ExplorerTreeNodeViewModel> OpenWorkspaces => ExplorerRoots;

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private ChatModelOptionViewModel? _selectedModel;

    [ObservableProperty]
    private bool _isModelDropdownOpen;

    [ObservableProperty]
    private bool _isModelDropdownHighlighted;

    [ObservableProperty]
    private bool _hasExplicitModelSelection;

    [ObservableProperty]
    private string _modelDropdownStatusText = string.Empty;

    [ObservableProperty]
    private bool _isAgentDropdownOpen;

    [ObservableProperty]
    private bool _isAgentDropdownHighlighted;

    [ObservableProperty]
    private string _endpoint = "runtime://bitnet -> ./runtime/NexusCentral-BitNet-Runtime";

    [ObservableProperty]
    private ExplorerTreeNodeViewModel? _selectedExplorerNode;

    [ObservableProperty]
    private string _workspacePathInput = string.Empty;

    [ObservableProperty]
    private string _workspaceStatusText = string.Empty;

    [ObservableProperty]
    private string _sandboxExportPathInput = string.Empty;

    [ObservableProperty]
    private string _webLabStatusText = "Nexus Web Lab offline";

    [ObservableProperty]
    private bool _isThinking;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private int _maxTokens = 1200;

    [ObservableProperty]
    private string _generationStatusText = "Idle";

    [ObservableProperty]
    private bool _isInputHighlighted;

    [ObservableProperty]
    private bool _isSendButtonHighlighted;

    [ObservableProperty]
    private bool _isAttachButtonHighlighted;

    [ObservableProperty]
    private bool _isReloadButtonHighlighted;

    [ObservableProperty]
    private bool _isMaxTokensHighlighted;

    [ObservableProperty]
    private string _sessionId = string.Empty;

    [ObservableProperty]
    private string _latestRunId = string.Empty;

    [ObservableProperty]
    private string _selectedExecutionMode = "wrapper";

    [ObservableProperty]
    private WorkflowLibraryItemViewModel? _selectedWorkflow;

    [ObservableProperty]
    private TemplateDefinition? _selectedTemplate;

    [ObservableProperty]
    private WrapperDefinition? _selectedWrapper;

    [ObservableProperty]
    private string _artifactInputsText = string.Empty;

    [ObservableProperty]
    private bool _canAttachArtifacts;

    [ObservableProperty]
    private bool _showArtifactInputBox;

    [ObservableProperty]
    private string _selectedTemplateStatusText = "Template: Direct Chat";

    [ObservableProperty]
    private string _selectedWorkflowStatusText = "Workflow: none";

    [ObservableProperty]
    private string _queueStatusText = "Queue empty";

    [ObservableProperty]
    private string _previewFileContent = string.Empty;

    [ObservableProperty]
    private string _previewFileName = string.Empty;

    [ObservableProperty]
    private bool _isPreviewVisible;

    public ObservableCollection<WrapperDefinition> AvailableAgents => AvailableWrappers;
    public WorkspaceDefinition? SelectedWorkspace =>
        _services.Workspaces.FindWorkspace(_selectedWorkspaceId)
        ?? _services.Workspaces.GetActiveWorkspace();
    public ExplorerTreeNodeViewModel? SelectedWorkspaceNode =>
        SelectedExplorerNode is { IsWorkspaceRoot: false } node ? node : null;
    public string ActiveWorkspaceRootDirectory => SelectedWorkspace?.RootDirectory ?? _services.Paths.ResolvedRootDirectory;
    public string ActiveWorkspaceName => SelectedWorkspace?.Name ?? "Workspace";
    public string ActiveWorkspacePath => SelectedWorkspace?.RootDirectory ?? _services.Paths.ResolvedRootDirectory;
    public string SelectedWorkspaceTargetPath => SelectedWorkspaceNode?.FullPath ?? ActiveWorkspaceRootDirectory;
    public bool CanSaveSandbox => string.Equals(SelectedWorkspace?.Kind, "sandbox", StringComparison.OrdinalIgnoreCase);
    public bool CanDeleteSelectedSandbox => CanSaveSandbox && !string.Equals(SelectedWorkspace?.Id, WorkspaceService.SandboxWorkspaceId, StringComparison.OrdinalIgnoreCase);
    public bool ShowAgentSelector => true;
    public string ShortSessionId => ShortenIdentifier(SessionId);
    public string ShortLatestRunId => string.IsNullOrWhiteSpace(LatestRunId) ? "none" : ShortenIdentifier(LatestRunId);
    public string EffectiveMaxTokensStatusText => $"{MaxTokens} tokens";
    public bool HasQueuedItems => PendingQueueItems.Any();
    public IEnumerable<ChatQueueItemViewModel> PendingQueueItems => QueueItems.Where(i => i.IsQueued || i.IsRunning);
    public string SelectedWorkspaceTargetStatusText => string.IsNullOrWhiteSpace(SelectedWorkspaceTargetPath)
        ? "Target: none"
        : $"Target: {SelectedWorkspaceTargetPath}";

    public WrapperDefinition? SelectedAgent
    {
        get => SelectedWrapper;
        set => SelectedWrapper = value;
    }

    public ChatViewModel(AppServices services)
    {
        _services = services;
        SessionId = Guid.NewGuid().ToString("N");
        _services.Catalog.CatalogChanged += RefreshAvailableModels;
        _services.Library.ChatInstancesChanged += RefreshAvailableModels;
        _services.Workflows.WorkflowsChanged += RefreshAvailableWorkflows;
        _services.Templates.TemplatesChanged += RefreshAvailableTemplates;
        _services.Wrappers.WrappersChanged += RefreshAvailableWrappers;
        _services.Workspaces.WorkspacesChanged += RefreshOpenWorkspaces;
        _services.WebLab.StatusChanged += () => Dispatcher.UIThread.Post(SyncWebLabStatus);
        _services.AgentConfigs.ConfigurationsChanged += ApplyCurrentAgentDefaults;
        _services.UiAutomation.Changed += SyncAutomationState;
        QueueItems.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (ChatQueueItemViewModel item in e.NewItems)
                    item.PropertyChanged += (_, _) => { OnPropertyChanged(nameof(HasQueuedItems)); OnPropertyChanged(nameof(PendingQueueItems)); };
            }
            OnPropertyChanged(nameof(HasQueuedItems));
            OnPropertyChanged(nameof(PendingQueueItems));
        };
        Messages.CollectionChanged += (_, _) =>
        {
            RefreshMessageTransitions();
            if (!_loadingWorkspaceSession)
            {
                PersistActiveWorkspaceSession();
            }
        };
        RefreshAvailableWorkflows();
        RefreshAvailableTemplates();
        RefreshAvailableWrappers();
        RefreshAvailableModels();
        RefreshOpenWorkspaces();
        SyncAutomationState();
        SyncWebLabStatus();
    }

    public ChatRunState? GetRun(string runId)
    {
        lock (_runLock)
        {
            return _runs.TryGetValue(runId, out var run) ? run.Clone() : null;
        }
    }

    public ChatRunState? GetLatestRun()
    {
        lock (_runLock)
        {
            if (string.IsNullOrWhiteSpace(LatestRunId))
            {
                return null;
            }

            return _runs.TryGetValue(LatestRunId, out var run) ? run.Clone() : null;
        }
    }

    public int PruneCompletedQueueItems(int retainCount)
    {
        var normalizedRetainCount = Math.Max(0, retainCount);
        var removableItems = QueueItems
            .Where(item => !item.IsQueued && !item.IsRunning)
            .OrderByDescending(item => item.CompletedAtUtc ?? item.StartedAtUtc ?? item.CreatedAtUtc)
            .Skip(normalizedRetainCount)
            .ToList();
        if (removableItems.Count == 0)
        {
            return 0;
        }

        foreach (var item in removableItems)
        {
            QueueItems.Remove(item);
            lock (_runLock)
            {
                _runs.Remove(item.RunId);
                _runStartSignals.Remove(item.RunId);
            }
        }

        RefreshLatestRunReference();
        UpdateQueueStatus();
        return removableItems.Count;
    }

    public int ClearCompletedQueueItems()
    {
        return PruneCompletedQueueItems(0);
    }

    public ChatSnapshot GetSnapshot()
    {
        var directPresentation = string.Equals(SelectedExecutionMode, "direct", StringComparison.OrdinalIgnoreCase);
        var latestRun = GetLatestRun();
        var latestDirectRun = latestRun is not null && string.Equals(latestRun.ExecutionMode, "direct", StringComparison.OrdinalIgnoreCase)
            ? latestRun
            : null;
        var currentDirectPlan = directPresentation ? ResolveCurrentDirectChatPlan() : null;
        var latestAssistant = Messages.LastOrDefault(message => string.Equals(message.Role, "ai", StringComparison.OrdinalIgnoreCase) || string.Equals(message.Role, "error", StringComparison.OrdinalIgnoreCase));
        var latestThinkingMessage = directPresentation
            ? null
            : Messages.LastOrDefault(message =>
                message.IsExecutionFeed ||
                message.IsThinkingPhase ||
                (message.IsAnswerPhase && message.HasThinkingText));
        var latestAnswerMessage = Messages.LastOrDefault(message => message.IsAnswerPhase || message.IsPlainChatPresentation);
        var latestThinkingText = directPresentation || latestThinkingMessage is null
            ? string.Empty
            : latestThinkingMessage.IsExecutionFeed
                ? latestThinkingMessage.ExecutionFeedTranscript
                : latestThinkingMessage.IsStreamingThinking
                ? latestThinkingMessage.LiveThinkingText
                : !string.IsNullOrWhiteSpace(latestThinkingMessage.ThinkingText)
                    ? latestThinkingMessage.ThinkingText
                    : latestThinkingMessage.LiveThinkingText;
        var latestAnswerText = latestAnswerMessage is null
            ? string.Empty
            : latestAnswerMessage.IsStreamingAnswer
                ? directPresentation
                    ? string.Empty
                    : latestAnswerMessage.LiveAnswerText
                : latestAnswerMessage.Text;
        var latestSummarySource = !string.IsNullOrWhiteSpace(latestAnswerText)
            ? latestAnswerText
            : directPresentation && latestAnswerMessage?.IsStreamingAnswer == true
                ? latestAnswerMessage.Status
            : !string.IsNullOrWhiteSpace(latestThinkingText)
                ? latestThinkingText
                : latestAssistant is null
                    ? string.Empty
                    : latestAssistant.IsStreaming
                        ? (!string.IsNullOrWhiteSpace(latestAssistant.LiveAnswerText) ? latestAssistant.LiveAnswerText : latestAssistant.LiveThinkingText)
                        : latestAssistant.Text;
        var latestStreamPhase = latestAnswerMessage is not null && latestAnswerMessage.IsStreamingAnswer
            ? "answer"
            : latestThinkingMessage is not null && latestThinkingMessage.IsExecutionFeedStreaming
                ? "feed"
                : latestThinkingMessage is not null && latestThinkingMessage.IsStreamingThinking
                ? "thinking"
                : string.Empty;
        var latestThinkingStatus = latestThinkingMessage?.Status ?? string.Empty;
        var latestAnswerStatus = latestAnswerMessage?.Status ?? string.Empty;
        var latestThinkingIsStreaming = latestThinkingMessage?.IsStreaming ?? false;
        var latestAnswerIsStreaming = latestAnswerMessage?.IsStreamingAnswer ?? false;

        return new ChatSnapshot(
            SelectedExecutionMode,
            SelectedTemplate?.Id ?? string.Empty,
            SelectedTemplate?.Name ?? "Direct Chat",
            SelectedWorkflow?.Id ?? string.Empty,
            SelectedWorkflow?.Name ?? string.Empty,
            SelectedWrapper?.Id ?? string.Empty,
            SelectedWrapper?.Name ?? string.Empty,
            SelectedTemplate?.Inputs.Select(input => input.Kind).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>(),
            ArtifactInputsText,
            SessionId,
            SelectedModel?.ModelId ?? string.Empty,
            SelectedModel?.InstanceId ?? string.Empty,
            SelectedModel?.DisplayName ?? "Automatic (best available model)",
            SelectedModel?.RuntimeBackend ?? "automatic",
            SelectedModel?.IsRunnable ?? false,
            currentDirectPlan?.ResolvedModelId ?? latestDirectRun?.ResolvedModelId ?? string.Empty,
            currentDirectPlan?.ResolvedInstanceId ?? latestDirectRun?.ResolvedInstanceId ?? string.Empty,
            currentDirectPlan?.ResolvedExecutionMode ?? latestDirectRun?.ResolvedExecutionMode ?? string.Empty,
            latestDirectRun?.OutputQualityState ?? string.Empty,
            LatestRunId,
            InputText);
    }

    [RelayCommand]
    private async Task SendMessage()
    {
        if (!TryQueueRun(InputText, clearInput: true, out var runId, out _))
        {
            return;
        }

        await WaitForRunToStartAsync(runId);
    }

    [RelayCommand]
    private void ReloadConversation()
    {
        Messages.Clear();
        InputText = string.Empty;
        IsThinking = false;
        IsGenerating = false;
        GenerationStatusText = "Idle";
        SessionId = Guid.NewGuid().ToString("N");
        ArtifactInputsText = string.Empty;
        lock (_runLock)
        {
            _runs.Clear();
        }
        QueueItems.Clear();
        SelectedWorkflow = null;
        QueueStatusText = "Queue empty";
        if (!string.IsNullOrWhiteSpace(_selectedWorkspaceId))
        {
            SelectWorkspaceRoot(_selectedWorkspaceId, persistSelection: false);
        }

        _services.UiAutomation.Highlight(ReloadButtonElementId);
        AddWelcomeMessage();
        PersistActiveWorkspaceSession();
    }

    [RelayCommand]
    private void ToggleWrapperSetting(WrapperSettingViewModel? setting)
    {
        if (setting is null)
        {
            return;
        }

        setting.Toggle();
        PersistActiveWorkspaceSession();
    }

    public bool TrySetExposedAgentSetting(string settingId, string value, out string error)
    {
        error = string.Empty;
        var setting = ExposedAgentSettings.FirstOrDefault(item => string.Equals(item.Id, settingId, StringComparison.OrdinalIgnoreCase));
        if (setting is null)
        {
            error = $"Agent setting '{settingId}' was not found.";
            return false;
        }

        setting.Value = value ?? string.Empty;
        PersistActiveWorkspaceSession();
        return true;
    }

    [RelayCommand]
    private void OpenWorkspaceFromPath()
    {
        if (string.IsNullOrWhiteSpace(WorkspacePathInput))
        {
            WorkspaceStatusText = "Workspace path required.";
            return;
        }

        try
        {
            var workspace = _services.Workspaces.OpenWorkspace(WorkspacePathInput);
            WorkspacePathInput = string.Empty;
            WorkspaceStatusText = $"Opened workspace {workspace.Name}.";
            SelectWorkspaceById(workspace.Id);
        }
        catch (Exception ex)
        {
            WorkspaceStatusText = ex.Message;
        }
    }

    [RelayCommand]
    private void CreateSandboxWorkspace()
    {
        TryCreateSandbox(null, out _);
    }

    [RelayCommand]
    private void DeleteSandboxWorkspace(ExplorerTreeNodeViewModel? node)
    {
        var workspaceId = node?.WorkspaceId ?? SelectedWorkspace?.Id ?? string.Empty;
        TryDeleteSandbox(workspaceId, out _);
    }

    [RelayCommand]
    private void ArchiveActiveWorkspaceChat()
    {
        if (SelectedWorkspace is null)
        {
            WorkspaceStatusText = "No workspace selected.";
            return;
        }

        PersistActiveWorkspaceSession();
        var archivePath = _services.Workspaces.ArchiveSession(SelectedWorkspace.Id);
        WorkspaceStatusText = $"Archived chat to {archivePath}.";
        LoadWorkspaceSession(SelectedWorkspace.Id);
    }

    [RelayCommand]
    private void RefreshWorkspaceTree()
    {
        var selectedRoot = FindExplorerRoot(SelectedWorkspace?.Id ?? string.Empty);
        if (selectedRoot is null)
        {
            WorkspaceStatusText = "Workspace tree refreshed.";
            return;
        }

        var expansionState = CaptureExpansionState();
        selectedRoot.ChildrenLoaded = false;
        if (selectedRoot.IsExpanded)
        {
            LoadNodeChildren(selectedRoot, forceReload: true, expansionState);
        }

        RebuildVisibleExplorerNodes();
        if (SelectedWorkspaceNode is not null)
        {
            RestoreSelectedExplorerNode(selectedRoot.WorkspaceId, SelectedWorkspaceNode.FullPath, persistSelection: false, fallbackToRoot: true);
        }
        else
        {
            SelectWorkspaceRoot(selectedRoot.WorkspaceId, persistSelection: false);
        }

        WorkspaceStatusText = $"Workspace tree refreshed for {selectedRoot.WorkspaceName}.";
    }

    [RelayCommand]
    private void ToggleExplorerNode(ExplorerTreeNodeViewModel? node)
    {
        if (node is null || !node.IsDirectory)
        {
            return;
        }

        var expanding = !node.IsExpanded;
        node.IsExpanded = expanding;
        if (expanding)
        {
            LoadNodeChildren(node, forceReload: false);
        }
        else if (SelectedExplorerNode is not null && IsNodeWithinSubtree(SelectedExplorerNode, node))
        {
            SetSelectedExplorerNodeInternal(node, persistSelection: false);
        }

        RebuildVisibleExplorerNodes();
        PersistActiveWorkspaceSession();
    }

    public async Task SaveSandboxToDirectoryAsync(string targetDirectory)
    {
        try
        {
            PersistActiveWorkspaceSession();
            var exported = await Task.Run(() => _services.Workspaces.SaveSandboxToDirectory(targetDirectory));
            WorkspaceStatusText = $"Sandbox exported to {exported.RootDirectory}.";
            RefreshOpenWorkspaces();
            SelectWorkspaceById(exported.Id);
        }
        catch (Exception ex)
        {
            WorkspaceStatusText = ex.Message;
        }
    }

    public bool TrySaveSandboxToDirectory(string? targetDirectory, out string error)
    {
        try
        {
            PersistActiveWorkspaceSession();
            var exported = _services.Workspaces.SaveSandboxToDirectory(targetDirectory);
            WorkspaceStatusText = $"Sandbox exported to {exported.RootDirectory}.";
            RefreshOpenWorkspaces();
            SelectWorkspaceById(exported.Id);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            WorkspaceStatusText = ex.Message;
            error = ex.Message;
            return false;
        }
    }

    [RelayCommand]
    private async Task SaveSandboxWorkspaceAsync()
    {
        await SaveSandboxToDirectoryAsync(SandboxExportPathInput);
    }

    [RelayCommand]
    private async Task OpenNexusWebLabAsync()
    {
        var started = await _services.WebLab.StartAsync(openBrowser: true);
        WebLabStatusText = started
            ? $"Nexus Web Lab ready at {_services.WebLab.AdvertisedUrl}"
            : "Failed to start Nexus Web Lab.";
    }

    [RelayCommand]
    private async Task CopyNexusWebLabUrlAsync()
    {
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(_services.WebLab.AdvertisedUrl);
                WebLabStatusText = $"Copied Nexus Web Lab URL: {_services.WebLab.AdvertisedUrl}";
            }
        }
        catch (Exception ex)
        {
            WebLabStatusText = ex.Message;
        }
    }

    public bool TryOpenWorkspace(string? path, out string error)
    {
        try
        {
            var workspace = _services.Workspaces.OpenWorkspace(path);
            WorkspacePathInput = string.Empty;
            SelectWorkspaceById(workspace.Id);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryCreateSandbox(string? name, out string error)
    {
        try
        {
            PersistActiveWorkspaceSession();
            var workspace = _services.Workspaces.CreateSandbox(name);
            WorkspaceStatusText = $"Created sandbox {workspace.Name}.";
            RefreshOpenWorkspaces();
            SelectWorkspaceById(workspace.Id);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            WorkspaceStatusText = ex.Message;
            error = ex.Message;
            return false;
        }
    }

    public bool TryDeleteSandbox(string? workspaceId, out string error)
    {
        try
        {
            var activeBeforeDelete = SelectedWorkspace?.Id ?? string.Empty;
            if (string.Equals(activeBeforeDelete, workspaceId, StringComparison.OrdinalIgnoreCase))
            {
                PersistActiveWorkspaceSession();
            }

            var deleted = _services.Workspaces.DeleteSandbox(workspaceId);
            WorkspaceStatusText = $"Deleted sandbox {deleted.Name}.";
            RefreshOpenWorkspaces();
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            WorkspaceStatusText = ex.Message;
            error = ex.Message;
            return false;
        }
    }

    public bool TrySelectWorkspace(string? workspaceId, out string error)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            error = "Workspace id is required.";
            return false;
        }

        if (_services.Workspaces.FindWorkspace(workspaceId) is null)
        {
            error = $"Workspace '{workspaceId}' was not found.";
            return false;
        }

        if (!ActivateWorkspaceAndSelect(workspaceId, targetPath: null, selectRoot: true))
        {
            error = $"Workspace '{workspaceId}' was not found.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TrySelectWorkspaceNode(string? workspaceId, string? fullPath, out string error)
    {
        if (!TrySelectWorkspace(workspaceId, out error))
        {
            return false;
        }

        if (SelectedWorkspace is null)
        {
            error = "No workspace selected.";
            return false;
        }

        if (ActivateWorkspaceAndSelect(workspaceId!, fullPath, selectRoot: string.IsNullOrWhiteSpace(fullPath)))
        {
            error = string.Empty;
            return true;
        }

        error = $"Path '{fullPath}' was not found in workspace '{SelectedWorkspace.Name}'.";
        return false;
    }

    private bool ActivateWorkspaceAndSelect(string workspaceId, string? targetPath, bool selectRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return false;
        }

        var previousWorkspaceId = SelectedWorkspace?.Id ?? string.Empty;
        if (!string.Equals(previousWorkspaceId, workspaceId, StringComparison.OrdinalIgnoreCase) && !_loadingWorkspaceSession)
        {
            PersistActiveWorkspaceSession();
        }

        if (!_services.Workspaces.SelectWorkspace(workspaceId))
        {
            return false;
        }

        _selectedWorkspaceId = workspaceId;
        RefreshOpenWorkspaces();
        LoadWorkspaceSession(workspaceId);

        if (selectRoot)
        {
            return SelectWorkspaceRoot(workspaceId, persistSelection: true);
        }

        return RestoreSelectedExplorerNode(workspaceId, targetPath ?? string.Empty, persistSelection: true, fallbackToRoot: true);
    }

    public void SyncWebLabStatus()
    {
        WebLabStatusText = _services.WebLab.StatusText;
    }

    public async Task<string> StartSendFromInputAsync()
    {
        var text = InputText;
        if (!TryQueueRun(text, clearInput: true, out var runId, out var error))
        {
            throw new InvalidOperationException(error);
        }

        await WaitForRunToStartAsync(runId);
        return runId;
    }

    public bool TryStartSend(string text, out string runId, out string error) =>
        TryQueueRun(text, clearInput: false, forcedArtifactInputs: null, out runId, out error);

    public bool TryStartSend(string text, IReadOnlyList<string> artifactInputs, out string runId, out string error) =>
        TryQueueRun(text, clearInput: false, forcedArtifactInputs: artifactInputs, out runId, out error);

    public void SetInputTextFromTool(string text)
    {
        InputText = text ?? string.Empty;
        _services.UiAutomation.Highlight(InputElementId);
    }

    public void TypeInputTextFromTool(string text)
    {
        InputText = string.Concat(InputText, text ?? string.Empty);
        _services.UiAutomation.Highlight(InputElementId);
    }

    public void SetArtifactInputsTextFromTool(string text)
    {
        ArtifactInputsText = text ?? string.Empty;
        ShowArtifactInputBox = !string.IsNullOrWhiteSpace(ArtifactInputsText);
        _services.UiAutomation.Highlight(ArtifactInputsElementId);
    }

    public void TypeArtifactInputsTextFromTool(string text)
    {
        ArtifactInputsText = string.Concat(ArtifactInputsText, text ?? string.Empty);
        ShowArtifactInputBox = !string.IsNullOrWhiteSpace(ArtifactInputsText);
        _services.UiAutomation.Highlight(ArtifactInputsElementId);
    }

    [RelayCommand]
    private void ToggleAttachmentTray()
    {
        if (!CanAttachArtifacts)
        {
            ShowArtifactInputBox = false;
            return;
        }

        ShowArtifactInputBox = !ShowArtifactInputBox;
        _services.UiAutomation.SetExpanded(AttachButtonElementId, ShowArtifactInputBox);
        _services.UiAutomation.Highlight(AttachButtonElementId);
    }

    [RelayCommand]
    private void InsertComposerText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        InputText = string.IsNullOrWhiteSpace(InputText)
            ? text.TrimStart()
            : $"{InputText.TrimEnd()} {text.TrimStart()}";
        _services.UiAutomation.Highlight(InputElementId);
        ChatView.SyncSemanticInputText(InputText);
    }

    public void SetMaxTokensFromTool(int value)
    {
        var bounded = Math.Clamp(value, 100, 5000);
        var instanceId = !string.IsNullOrWhiteSpace(SelectedModel?.InstanceId)
            ? SelectedModel.InstanceId
            : ResolveAutoChatInstance()?.InstanceId;
        if (!string.IsNullOrWhiteSpace(instanceId))
        {
            _services.Library.UpdateInstanceTokens(instanceId, bounded);
        }

        MaxTokens = bounded;
        _services.UiAutomation.Highlight(MaxTokensElementId);
    }

    public bool ClickButton(string elementId, out string? runId, out string error)
    {
        runId = null;
        error = string.Empty;

        if (string.Equals(elementId, SendButtonElementId, StringComparison.OrdinalIgnoreCase))
        {
            if (!TryQueueRun(InputText, clearInput: true, out var queuedRunId, out error))
            {
                return false;
            }

            runId = queuedRunId;
            _services.UiAutomation.Highlight(SendButtonElementId);
            return true;
        }

        if (string.Equals(elementId, ReloadButtonElementId, StringComparison.OrdinalIgnoreCase))
        {
            ReloadConversation();
            return true;
        }

        if (string.Equals(elementId, AttachButtonElementId, StringComparison.OrdinalIgnoreCase))
        {
            ToggleAttachmentTray();
            _services.UiAutomation.Highlight(AttachButtonElementId);
            return true;
        }

        error = $"Button '{elementId}' is not supported.";
        return false;
    }

    public bool ToggleModelDropdown()
    {
        _services.UiAutomation.FocusElement(ModelDropdownElementId, !_services.UiAutomation.IsExpanded(ModelDropdownElementId));
        return true;
    }

    public bool ToggleAgentDropdown()
    {
        _services.UiAutomation.FocusElement(AgentDropdownElementId, !_services.UiAutomation.IsExpanded(AgentDropdownElementId));
        return true;
    }

    public bool ToggleElement(string elementId, out string error)
    {
        error = string.Empty;

        if (string.Equals(elementId, ModelDropdownElementId, StringComparison.OrdinalIgnoreCase))
        {
            ToggleModelDropdown();
            return true;
        }

        if (string.Equals(elementId, AgentDropdownElementId, StringComparison.OrdinalIgnoreCase))
        {
            ToggleAgentDropdown();
            return true;
        }

        if (string.Equals(elementId, AttachButtonElementId, StringComparison.OrdinalIgnoreCase))
        {
            ToggleAttachmentTray();
            return true;
        }

        if (string.Equals(elementId, ArtifactInputsElementId, StringComparison.OrdinalIgnoreCase))
        {
            ShowArtifactInputBox = true;
            _services.UiAutomation.Highlight(ArtifactInputsElementId);
            return true;
        }

        error = $"Element '{elementId}' is not supported.";
        return false;
    }

    public bool TrySelectModelOption(string? itemId, string? label, out string error)
    {
        var candidate = AvailableModels.FirstOrDefault(option =>
            (!string.IsNullOrWhiteSpace(itemId) && (
                string.Equals(option.InstanceId, itemId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(option.ModelId, itemId, StringComparison.OrdinalIgnoreCase))) ||
            (!string.IsNullOrWhiteSpace(label) && string.Equals(option.DisplayName, label, StringComparison.OrdinalIgnoreCase)));

        if (candidate is null)
        {
            error = "No matching model option was found.";
            return false;
        }

        SelectedModel = candidate;
        _services.UiAutomation.FocusElement(ModelDropdownElementId, false);
        error = string.Empty;
        return true;
    }

    public bool TrySelectAgentOption(string? itemId, string? label, out string error)
    {
        var candidate = AvailableAgents.FirstOrDefault(agent =>
            (!string.IsNullOrWhiteSpace(itemId) && string.Equals(agent.Id, itemId, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(label) && string.Equals(agent.Name, label, StringComparison.OrdinalIgnoreCase)));

        if (candidate is null)
        {
            error = "No matching agent option was found.";
            return false;
        }

        _applyingAgentSelection = true;
        try
        {
            SelectedAgent = candidate;
            SelectedWorkflow = null;
        }
        finally
        {
            _applyingAgentSelection = false;
        }

        _services.UiAutomation.FocusElement(AgentDropdownElementId, false);
        error = string.Empty;
        return true;
    }

    public bool TrySelectWrapperOption(string? itemId, string? label, out string error)
    {
        return TrySelectAgentOption(itemId, label, out error);
    }

    public bool IsToolElementVisible(string elementId) =>
        (ShowAgentSelector && string.Equals(elementId, AgentDropdownElementId, StringComparison.OrdinalIgnoreCase)) ||
        (CanAttachArtifacts && string.Equals(elementId, AttachButtonElementId, StringComparison.OrdinalIgnoreCase)) ||
        (ShowArtifactInputBox && CanAttachArtifacts && string.Equals(elementId, ArtifactInputsElementId, StringComparison.OrdinalIgnoreCase)) ||
        string.Equals(elementId, ReloadButtonElementId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(elementId, InputElementId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(elementId, SendButtonElementId, StringComparison.OrdinalIgnoreCase) ||
        false;

    private async Task ExecuteDirectRunAsync(string runId, string userText, string selectedInstanceId, string selectedModelId, string selectedModelLabel)
    {
        var userMessage = new ChatMessageViewModel("user", userText);
        Messages.Add(userMessage);
        ChatView.RequestTranscriptScrollToBottom();

        var answerMessage = new ChatMessageViewModel("ai", string.Empty);
        answerMessage.MarkPlainChatPresentation();
        answerMessage.UpdateStatus("Typing", "TokyoAccentBrush");
        Messages.Add(answerMessage);
        ChatView.RequestTranscriptScrollToBottom();

        IsThinking = false;
        IsGenerating = true;
        GenerationStatusText = "Typing";
        UpdateRun(runId, state =>
        {
            state.Status = "running";
            state.StartedAtUtc = DateTimeOffset.UtcNow;
            state.IsStreaming = true;
            state.StreamPhase = "answer";
            state.TranscriptMessageCount = Messages.Count;
            state.LiveThinkingText = string.Empty;
            state.LiveAnswerText = string.Empty;
            state.LastStreamUpdateUtc = DateTimeOffset.UtcNow;
        });
        SignalRunStarted(runId);
        var plan = _services.DirectChatExecutionPolicy.Resolve(selectedInstanceId, selectedModelId, MaxTokens);
        if (!plan.IsRunnable)
        {
            var unavailableMessage = string.IsNullOrWhiteSpace(plan.FailureReason)
                ? "Load and activate a model in Models before using direct chat."
                : plan.FailureReason;
            answerMessage.ClearLiveStream();
            answerMessage.Role = "error";
            answerMessage.SetText(unavailableMessage);
            answerMessage.UpdateStatus("Error", "TokyoErrorBrush");
            GenerationStatusText = "Error";
            UpdateRun(runId, state =>
            {
                state.Status = "error";
                state.CompletedAtUtc = DateTimeOffset.UtcNow;
                state.FinishReason = "not_runnable";
                state.Error = unavailableMessage;
                state.OutputPreview = BuildPreview(unavailableMessage);
                state.SelectedInstanceId = selectedInstanceId;
                state.SelectedModelId = selectedModelId;
                state.SelectedModelLabel = selectedModelLabel;
                state.OutputQualityState = "not_runnable";
                state.IsStreaming = false;
                state.StreamPhase = "done";
                state.LiveThinkingText = string.Empty;
                state.LiveAnswerText = unavailableMessage;
                state.TranscriptMessageCount = Messages.Count;
                state.LastStreamUpdateUtc = DateTimeOffset.UtcNow;
            });
            _services.Logs.Append("warn", "chat", $"Direct run '{runId}' failed before launch: {unavailableMessage}", runId);
            ChatView.RequestTranscriptScrollToBottom();
            IsThinking = false;
            IsGenerating = false;
            PersistActiveWorkspaceSession();
            RemoveRunSignal(runId);
            return;
        }

        UpdateRun(runId, state =>
        {
            state.Status = "running";
            state.TranscriptMessageCount = Messages.Count;
            state.LiveThinkingText = string.Empty;
            state.LiveAnswerText = string.Empty;
            state.SelectedInstanceId = selectedInstanceId;
            state.SelectedModelId = selectedModelId;
            state.SelectedModelLabel = selectedModelLabel;
            state.ResolvedInstanceId = plan.ResolvedInstanceId;
            state.ResolvedModelId = plan.ResolvedModelId;
            state.ResolvedExecutionMode = plan.ResolvedExecutionMode;
            state.OutputQualityState = "pending";
            state.ModelId = plan.ResolvedModelId;
            state.ModelLabel = plan.ResolvedModelLabel;
            state.Backend = plan.ResolvedBackend;
        });

        _services.Logs.Append(
            "info",
            "chat",
            $"Starting direct run '{runId}' with selected_instance='{selectedInstanceId}', selected_model='{selectedModelId}', resolved_instance='{plan.ResolvedInstanceId}', resolved_model='{plan.ResolvedModelId}', mode='{plan.ResolvedExecutionMode}', policy='{plan.PolicyName}'.",
            runId);

        var acceptStreamUpdates = true;
        using var streamProgress = new BatchedRuntimeStreamProgress(chunk =>
        {
            if (!acceptStreamUpdates)
            {
                return;
            }

            answerMessage.UpdateStatus("Typing", "TokyoAccentBrush");
            answerMessage.AppendLiveChunk(chunk);
            GenerationStatusText = "Typing";

            UpdateRun(runId, state =>
            {
                state.Status = "streaming_answer";
                state.StreamPhase = chunk.IsThinking ? "thinking" : "answer";
                state.IsStreaming = true;
                state.TranscriptMessageCount = Messages.Count;
                state.LiveThinkingText = answerMessage.LiveThinkingText;
                state.LiveAnswerText = answerMessage.LiveAnswerText;
                state.LastStreamUpdateUtc = DateTimeOffset.UtcNow;
            });
            ChatView.RequestTranscriptScrollToBottom();
        });

        var hasExplicitRuntimeTarget = !string.IsNullOrWhiteSpace(selectedInstanceId) || !string.IsNullOrWhiteSpace(selectedModelId);
        var runtimeInstanceId = hasExplicitRuntimeTarget ? plan.ResolvedInstanceId : null;
        var runtimeModelId = hasExplicitRuntimeTarget ? plan.ResolvedModelId : null;

        var attempt = 0;
        var attemptPrompt = userText;
        while (true)
        {
            acceptStreamUpdates = true;
            RuntimeInvocationResult result;
            if (string.Equals(plan.ResolvedExecutionMode, "thinking", StringComparison.OrdinalIgnoreCase))
            {
                result = await _services.Runtime.RunThinkingAsync(
                    attemptPrompt,
                    runtimeModelId,
                    attempt == 0 ? plan.InitialMaxTokens : plan.RetryMaxTokens,
                    runtimeInstanceId,
                    streamProgress);
            }
            else
            {
                result = await _services.Runtime.RunInstructAsync(
                    attemptPrompt,
                    runtimeModelId,
                    attempt == 0 ? plan.InitialMaxTokens : plan.RetryMaxTokens,
                    runtimeInstanceId,
                    streamProgress);
            }

            acceptStreamUpdates = false;
            await streamProgress.FlushAsync();
            var streamedFallbackText = !string.IsNullOrWhiteSpace(answerMessage.LiveAnswerText)
                ? answerMessage.LiveAnswerText
                : answerMessage.LiveThinkingText;
            var decision = _services.DirectChatQualityGuard.Evaluate(result, streamedFallbackText, plan, attempt);
            var backend = string.IsNullOrWhiteSpace(result.Backend) ? plan.ResolvedBackend : result.Backend;

            _services.Logs.Append(
                string.Equals(decision.Outcome, "accept", StringComparison.OrdinalIgnoreCase) ? "info" : "warn",
                "chat",
                $"Direct run '{runId}' attempt={attempt} outcome='{decision.Outcome}' quality='{decision.QualityState}' finish_reason='{result.FinishReason}' backend='{backend}' diagnostic='{decision.Diagnostic}'.",
                runId);

            if (string.Equals(decision.Outcome, "retry_with_tighter_prompt", StringComparison.OrdinalIgnoreCase))
            {
                attempt++;
                answerMessage.ClearLiveStream();
                answerMessage.SetText(string.Empty);
                answerMessage.UpdateStatus("Retrying", "TokyoWarningBrush");
                GenerationStatusText = "Retrying";
                UpdateRun(runId, state =>
                {
                    state.Status = "retrying";
                    state.ElapsedMs = result.ElapsedMs;
                    state.FinishReason = result.FinishReason;
                    state.TokensGenerated = result.TokensGenerated;
                    state.Backend = backend;
                    state.IsStreaming = false;
                    state.StreamPhase = "retrying";
                    state.LiveThinkingText = string.Empty;
                    state.LiveAnswerText = string.Empty;
                    state.LastStreamUpdateUtc = DateTimeOffset.UtcNow;
                    state.OutputQualityState = decision.QualityState;
                    state.RetryCount = attempt;
                });
                ChatView.RequestTranscriptScrollToBottom();
                attemptPrompt = _services.DirectChatExecutionPolicy.BuildRetryPrompt(userText);
                continue;
            }

            if (string.Equals(decision.Outcome, "fail_visible", StringComparison.OrdinalIgnoreCase))
            {
                var message = result.IsModelHealthError
                    ? $"Selected model unavailable: {result.Error}{Environment.NewLine}Use model repair or re-download before sending again."
                    : !string.IsNullOrWhiteSpace(decision.VisibleError)
                        ? decision.VisibleError
                        : $"Runtime error: {result.Error}";
                var statusText = result.IsModelHealthError ? "Repair Needed" : "Error";
                var statusBrush = result.IsModelHealthError ? "TokyoErrorBrush" : "TokyoErrorBrush";

                answerMessage.ClearLiveStream();
                answerMessage.Role = "error";
                answerMessage.SetText(message);
                answerMessage.UpdateStatus(statusText, statusBrush);
                GenerationStatusText = statusText;

                UpdateRun(runId, state =>
                {
                    state.Status = "error";
                    state.CompletedAtUtc = DateTimeOffset.UtcNow;
                    state.ElapsedMs = result.ElapsedMs;
                    state.FinishReason = string.IsNullOrWhiteSpace(result.FinishReason) ? "error" : result.FinishReason;
                    state.Error = message;
                    state.OutputPreview = BuildPreview(message);
                    state.SelectedInstanceId = selectedInstanceId;
                    state.SelectedModelId = selectedModelId;
                    state.SelectedModelLabel = selectedModelLabel;
                    state.ResolvedInstanceId = plan.ResolvedInstanceId;
                    state.ResolvedModelId = plan.ResolvedModelId;
                    state.ResolvedExecutionMode = plan.ResolvedExecutionMode;
                    state.ModelId = plan.ResolvedModelId;
                    state.ModelLabel = plan.ResolvedModelLabel;
                    state.Backend = backend;
                    state.OutputQualityState = decision.QualityState;
                    state.RetryCount = attempt;
                    state.IsStreaming = false;
                    state.StreamPhase = "done";
                    state.LiveThinkingText = string.Empty;
                    state.LiveAnswerText = message;
                    state.TranscriptMessageCount = Messages.Count;
                    state.LastStreamUpdateUtc = DateTimeOffset.UtcNow;
                });
                break;
            }

            var finalOutput = decision.FinalText;
            var audioPath = ExtractAudioPath(finalOutput);
            answerMessage.ClearLiveStream();
            answerMessage.HasAudio = audioPath is not null;
            answerMessage.AudioPath = audioPath;
            answerMessage.SetText(finalOutput);
            answerMessage.UpdateStatus("Done", "TokyoSuccessBrush");
            GenerationStatusText = "Done";

            UpdateRun(runId, state =>
            {
                state.Status = "done";
                state.CompletedAtUtc = DateTimeOffset.UtcNow;
                state.ElapsedMs = result.ElapsedMs;
                state.FinishReason = result.FinishReason;
                state.TokensGenerated = result.TokensGenerated;
                state.OutputPreview = BuildPreview(finalOutput);
                state.SelectedInstanceId = selectedInstanceId;
                state.SelectedModelId = selectedModelId;
                state.SelectedModelLabel = selectedModelLabel;
                state.ResolvedInstanceId = plan.ResolvedInstanceId;
                state.ResolvedModelId = plan.ResolvedModelId;
                state.ResolvedExecutionMode = plan.ResolvedExecutionMode;
                state.ModelId = string.IsNullOrWhiteSpace(result.ModelId) ? plan.ResolvedModelId : result.ModelId;
                state.ModelLabel = plan.ResolvedModelLabel;
                state.Backend = backend;
                state.OutputQualityState = decision.QualityState;
                state.RetryCount = attempt;
                state.IsStreaming = false;
                state.StreamPhase = "done";
                state.LiveThinkingText = string.Empty;
                state.LiveAnswerText = finalOutput;
                state.TranscriptMessageCount = Messages.Count;
                state.LastStreamUpdateUtc = DateTimeOffset.UtcNow;
            });
            break;
        }

        ChatView.RequestTranscriptScrollToBottom();

        IsThinking = false;
        IsGenerating = false;
        PersistActiveWorkspaceSession();
        RemoveRunSignal(runId);
    }

    private async Task ExecuteWrapperRunAsync(string runId, string userText, string selectedModelId, string selectedModelLabel)
    {
        var template = ResolveEffectiveTemplate();
        var wrapper = ResolveEffectiveWrapper();
        if (template is null || wrapper is null)
        {
            var errorMessage = template is null ? "No template is selected for wrapper mode." : "No wrapper is selected for wrapper mode.";
            var errorResponse = new ChatMessageViewModel("error", errorMessage);
            errorResponse.UpdateStatus("Error", "TokyoErrorBrush");
            Messages.Add(errorResponse);
            IsGenerating = false;
            IsThinking = false;
            GenerationStatusText = "Error";
            UpdateRun(runId, state =>
            {
                state.Status = "error";
                state.Error = errorMessage;
                state.OutputPreview = BuildPreview(errorMessage);
                state.CompletedAtUtc = DateTimeOffset.UtcNow;
            });
            SignalRunStarted(runId);
            PersistActiveWorkspaceSession();
            RemoveRunSignal(runId);
            return;
        }

        var userMessage = new ChatMessageViewModel("user", userText);
        Messages.Add(userMessage);
        ChatView.RequestTranscriptScrollToBottom();

        var feedMessage = CreateExecutionFeedMessage("Running Wrapper", "TokyoWarningBrush");
        Messages.Add(feedMessage);
        ChatView.RequestTranscriptScrollToBottom();

        AppendSelectedModelFeedEntry(feedMessage, selectedModelLabel);
        AppendFeedEntry(feedMessage, ChatExecutionFeedSection.Agent, "status", $"Workspace root: {ActiveWorkspaceRootDirectory}.");
        if (SelectedWorkspaceNode is not null)
        {
            AppendFeedEntry(feedMessage, ChatExecutionFeedSection.Agent, "status", $"Selected target: {SelectedWorkspaceTargetPath}.");
        }
        AppendFeedEntry(feedMessage, ChatExecutionFeedSection.Agent, "status", $"Launching agent '{wrapper.Name}'.");

        IsThinking = false;
        IsGenerating = true;
        GenerationStatusText = "Running Wrapper";
        UpdateRun(runId, state =>
        {
            state.Status = "running_wrapper";
            state.StartedAtUtc = DateTimeOffset.UtcNow;
            state.TemplateId = template.Id;
            state.TemplateName = template.Name;
            state.WrapperId = wrapper.Id;
            state.WrapperName = wrapper.Name;
            state.ExecutionMode = "wrapper";
            state.ModelLabel = selectedModelLabel;
            state.SelectedModelId = selectedModelId;
            state.ArtifactInputs = ParseArtifactInputs(ArtifactInputsText).ToArray();
            state.ArtifactInputs = ParseArtifactInputs(ArtifactInputsText).ToArray();
        });
        SignalRunStarted(runId);

        string finalText = string.Empty;
        string errorText = string.Empty;
        string terminalStatusText = string.Empty;
        var artifacts = new List<WrapperArtifact>();
        var wrapperSettings = GetWrapperSettingValues(wrapper);
        var wrapperMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["workspace_root"] = ActiveWorkspaceRootDirectory,
            ["root_directory"] = ActiveWorkspaceRootDirectory,
            ["working_directory"] = ActiveWorkspaceRootDirectory,
            ["selected_target_path"] = SelectedWorkspaceTargetPath,
            ["run_id"] = runId,
            ["session_id"] = SessionId,
            ["goal_context"] = userText,
        };
        foreach (var setting in wrapperSettings)
        {
            wrapperMetadata[$"wrapper_setting_{setting.Key}"] = setting.Value;
            if (!wrapperMetadata.ContainsKey(setting.Key))
            {
                wrapperMetadata[setting.Key] = setting.Value;
            }
        }

        var terminalStateSource = new TaskCompletionSource<WrapperRunState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var wrapperProgress = new Progress<WrapperRunStreamEvent>(streamEvent =>
        {
            switch (streamEvent.Type)
            {
                case "log":
                    AppendWrapperStreamFeedEvent(feedMessage, streamEvent, "log", streamEvent.Text);
                    break;
                case "status":
                    var statusText = string.IsNullOrWhiteSpace(streamEvent.Status) ? "Running Wrapper" : streamEvent.Status;
                    terminalStatusText = statusText;
                    feedMessage.UpdateStatus(statusText, ResolveStatusBrush(streamEvent, "TokyoWarningBrush"));
                    AppendWrapperStreamFeedEvent(feedMessage, streamEvent, "status", statusText);
                    GenerationStatusText = statusText;
                    if (string.Equals(statusText, "Completed", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(statusText, "Stopped", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.Equals(statusText, "Stopped", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(finalText))
                        {
                            terminalStateSource.TrySetResult(new WrapperRunState
                            {
                                Status = string.Equals(statusText, "Stopped", StringComparison.OrdinalIgnoreCase) ? "stopped" : "done",
                                FinalText = finalText,
                                Error = errorText,
                                CompletedAtUtc = DateTimeOffset.UtcNow,
                                ArtifactOutputs = artifacts.Select(artifact => new WrapperArtifact
                                {
                                    Label = artifact.Label,
                                    Path = artifact.Path,
                                    Kind = artifact.Kind
                                }).ToList()
                            });
                        }
                    }
                    break;
                case "artifact":
                    if (streamEvent.Artifact is not null && !artifacts.Any(existing => string.Equals(existing.Path, streamEvent.Artifact.Path, StringComparison.OrdinalIgnoreCase)))
                    {
                        artifacts.Add(streamEvent.Artifact);
                    }
                    if (streamEvent.Artifact is not null)
                    {
                        if (!string.IsNullOrWhiteSpace(streamEvent.ChainId))
                        {
                            AppendWrapperStreamFeedEvent(feedMessage, streamEvent, "artifact", $"{streamEvent.Artifact.Label} -> {streamEvent.Artifact.Path}");
                        }
                        else
                        {
                            AppendArtifactFeedEntry(feedMessage, streamEvent.Artifact, "info");
                        }
                    }
                    break;
                case "final":
                    finalText = streamEvent.Text ?? string.Empty;
                    AppendWrapperStreamFeedEvent(feedMessage, streamEvent, "final_marker", "Primary output ready.");
                    if (string.Equals(terminalStatusText, "Completed", StringComparison.OrdinalIgnoreCase))
                    {
                        terminalStateSource.TrySetResult(new WrapperRunState
                        {
                            Status = "done",
                            FinalText = finalText,
                            Error = errorText,
                            CompletedAtUtc = DateTimeOffset.UtcNow,
                            ArtifactOutputs = artifacts.Select(artifact => new WrapperArtifact
                            {
                                Label = artifact.Label,
                                Path = artifact.Path,
                                Kind = artifact.Kind
                            }).ToList()
                        });
                    }
                    break;
                case "error":
                    errorText = streamEvent.Text ?? string.Empty;
                    AppendWrapperStreamFeedEvent(feedMessage, streamEvent, "error", errorText, "error");
                    feedMessage.UpdateStatus("Error", "TokyoErrorBrush");
                    terminalStateSource.TrySetResult(new WrapperRunState
                    {
                        Status = "error",
                        Error = errorText,
                        FinalText = finalText,
                        CompletedAtUtc = DateTimeOffset.UtcNow,
                        ArtifactOutputs = artifacts.Select(artifact => new WrapperArtifact
                        {
                            Label = artifact.Label,
                            Path = artifact.Path,
                            Kind = artifact.Kind
                        }).ToList()
                    });
                    break;
            }

            UpdateRun(runId, state =>
            {
                state.LiveThinkingText = feedMessage.ExecutionFeedTranscript;
                state.LiveAnswerText = finalText;
                state.ArtifactOutputs = artifacts.Select(artifact => artifact.Path).ToArray();
                state.Error = errorText;
                state.TranscriptMessageCount = Messages.Count;
                state.LastStreamUpdateUtc = DateTimeOffset.UtcNow;
            });
            ChatView.RequestTranscriptScrollToBottom();
        });

        var handle = _services.WrapperRuns.StartRun(new WrapperRunRequest
        {
            TemplateId = template.Id,
            WrapperId = wrapper.Id,
            Mode = "wrapper",
            Prompt = userText,
            RootDirectory = ActiveWorkspaceRootDirectory,
            SessionId = SessionId,
            ArtifactInputs = ParseArtifactInputs(ArtifactInputsText),
            ModelId = selectedModelId,
            MaxTokens = MaxTokens,
            NexuscentralBaseUrl = "http://localhost:3005",
            LlmToolRoute = "/nexuscentral/thinking",
            Metadata = wrapperMetadata
        }, wrapperProgress);

        var completedTask = await Task.WhenAny(handle.Completion, terminalStateSource.Task);
        var wrapperState = completedTask == handle.Completion
            ? await handle.Completion
            : await terminalStateSource.Task;
        if (string.IsNullOrWhiteSpace(wrapperState.FinalText) && !string.IsNullOrWhiteSpace(finalText))
        {
            wrapperState.FinalText = finalText;
        }

        if (string.IsNullOrWhiteSpace(wrapperState.Error) && !string.IsNullOrWhiteSpace(errorText))
        {
            wrapperState.Error = errorText;
        }

        if (wrapperState.ArtifactOutputs.Count == 0 && artifacts.Count > 0)
        {
            wrapperState.ArtifactOutputs = artifacts.Select(artifact => new WrapperArtifact
            {
                Label = artifact.Label,
                Path = artifact.Path,
                Kind = artifact.Kind
            }).ToList();
        }
        feedMessage.SetFeedCompleted();
        feedMessage.UpdateStatus(
            string.Equals(wrapperState.Status, "done", StringComparison.OrdinalIgnoreCase) ? "Done" :
            string.Equals(wrapperState.Status, "stopped", StringComparison.OrdinalIgnoreCase) ? "Stopped" :
            "Error",
            string.Equals(wrapperState.Status, "done", StringComparison.OrdinalIgnoreCase) ? "TokyoSuccessBrush" :
            string.Equals(wrapperState.Status, "stopped", StringComparison.OrdinalIgnoreCase) ? "TokyoWarningBrush" :
            "TokyoErrorBrush");

        var answerRole = string.Equals(wrapperState.Status, "done", StringComparison.OrdinalIgnoreCase) ? "ai" : "error";
        var answerText = string.Equals(wrapperState.Status, "done", StringComparison.OrdinalIgnoreCase)
            ? (string.IsNullOrWhiteSpace(wrapperState.FinalText) ? "Wrapper run completed without final text." : wrapperState.FinalText)
            : (string.IsNullOrWhiteSpace(wrapperState.Error) ? $"Wrapper run ended with status '{wrapperState.Status}'." : wrapperState.Error);
        
        feedMessage.Role = answerRole;
        feedMessage.SetText(answerText);
        feedMessage.MarkAnswerPhase();
        feedMessage.SetArtifacts(wrapperState.ArtifactOutputs);

        GenerationStatusText = string.Equals(wrapperState.Status, "done", StringComparison.OrdinalIgnoreCase)
            ? "Done"
            : string.Equals(wrapperState.Status, "stopped", StringComparison.OrdinalIgnoreCase)
                ? "Stopped"
                : "Error";
        IsGenerating = false;
        IsThinking = false;

        UpdateRun(runId, state =>
        {
            state.Status = wrapperState.Status;
            state.CompletedAtUtc = wrapperState.CompletedAtUtc;
            state.OutputPreview = BuildPreview(answerText);
            state.Error = wrapperState.Error;
            state.ArtifactOutputs = wrapperState.ArtifactOutputs.Select(artifact => artifact.Path).ToArray();
            state.LiveThinkingText = feedMessage.ExecutionFeedTranscript;
            state.LiveAnswerText = answerText;
            state.TranscriptMessageCount = Messages.Count;
            state.LastStreamUpdateUtc = DateTimeOffset.UtcNow;
        });

        ChatView.RequestTranscriptScrollToBottom();
        PersistActiveWorkspaceSession();
        RemoveRunSignal(runId);
    }

    private async Task ExecuteWorkflowRunAsync(string runId, string userText, string selectedInstanceId, string selectedModelId, string selectedModelLabel, ChatDraft draft)
    {
        var workflowSelection = SelectedWorkflow;
        var workflow = workflowSelection is null ? null : _services.Workflows.FindById(workflowSelection.Id);
        if (workflow is null)
        {
            var errorMessage = workflowSelection is null ? "No workflow is selected for workflow mode." : $"Workflow '{workflowSelection.Id}' was not found.";
            var errorResponse = new ChatMessageViewModel("error", errorMessage);
            errorResponse.UpdateStatus("Error", "TokyoErrorBrush");
            Messages.Add(errorResponse);
            IsGenerating = false;
            IsThinking = false;
            GenerationStatusText = "Error";
            UpdateRun(runId, state =>
            {
                state.Status = "error";
                state.Error = errorMessage;
                state.OutputPreview = BuildPreview(errorMessage);
                state.CompletedAtUtc = DateTimeOffset.UtcNow;
            });
            SignalRunStarted(runId);
            PersistActiveWorkspaceSession();
            RemoveRunSignal(runId);
            return;
        }

        var userMessage = new ChatMessageViewModel("user", userText);
        Messages.Add(userMessage);
        ChatView.RequestTranscriptScrollToBottom();

        var feedMessage = CreateExecutionFeedMessage("Running Workflow", "TokyoWarningBrush");
        Messages.Add(feedMessage);
        ChatView.RequestTranscriptScrollToBottom();

        AppendSelectedModelFeedEntry(feedMessage, selectedModelLabel);
        AppendFeedEntry(feedMessage, ChatExecutionFeedSection.Workflow, "workflow", $"Workspace root: {ActiveWorkspaceRootDirectory}.");
        AppendFeedEntry(feedMessage, ChatExecutionFeedSection.Workflow, "workflow", $"Starting workflow '{workflow.Name}'.");

        IsThinking = false;
        IsGenerating = true;
        GenerationStatusText = "Running Workflow";
        UpdateRun(runId, state =>
        {
            state.Status = "running_workflow";
            state.StartedAtUtc = DateTimeOffset.UtcNow;
            state.TemplateId = workflow.Id;
            state.TemplateName = workflow.Name;
            state.WorkflowId = workflow.Id;
            state.WorkflowName = workflow.Name;
            state.ExecutionMode = "workflow";
            state.ModelLabel = selectedModelLabel;
            state.SelectedModelId = selectedModelId;
            state.SelectedInstanceId = selectedInstanceId;
            state.ArtifactInputs = draft.AttachmentPaths.ToArray();
        });
        SignalRunStarted(runId);

        workflow.WorkingDirectory = ActiveWorkspaceRootDirectory;
        var inputValues = BuildWorkflowInputValues(workflow, userText, draft);
        var tracker = new WorkflowFeedTracker();
        void HandleWorkflowUpdate(WorkflowRunState snapshot)
        {
            if (!string.Equals(snapshot.RunId, runId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                AppendWorkflowFeedDiff(feedMessage, tracker, snapshot);
                UpdateRun(runId, state =>
                {
                    state.LiveThinkingText = feedMessage.ExecutionFeedTranscript;
                    state.TranscriptMessageCount = Messages.Count;
                    state.LastStreamUpdateUtc = DateTimeOffset.UtcNow;
                });
                ChatView.RequestTranscriptScrollToBottom();
            });
        }

        _services.WorkflowRuns.RunUpdated += HandleWorkflowUpdate;
        var handle = _services.WorkflowRuns.StartRun(workflow, inputValues);
        WorkflowRunState workflowState;
        try
        {
            workflowState = await handle.Completion;
        }
        finally
        {
            _services.WorkflowRuns.RunUpdated -= HandleWorkflowUpdate;
        }
        var workflowArtifacts = workflowState.Steps.SelectMany(step => step.ArtifactOutputs).ToList();
        var summaryText = workflowState.Status == "done"
            ? BuildWorkflowSummary(workflowState)
            : string.IsNullOrWhiteSpace(workflowState.Error) ? $"Workflow ended with status '{workflowState.Status}'." : workflowState.Error;

        AppendWorkflowFeedDiff(feedMessage, tracker, workflowState);
        feedMessage.SetFeedCompleted();
        feedMessage.UpdateStatus(
            string.Equals(workflowState.Status, "done", StringComparison.OrdinalIgnoreCase) ? "Done" :
            string.Equals(workflowState.Status, "stopped", StringComparison.OrdinalIgnoreCase) ? "Stopped" :
            "Error",
            string.Equals(workflowState.Status, "done", StringComparison.OrdinalIgnoreCase) ? "TokyoSuccessBrush" :
            string.Equals(workflowState.Status, "stopped", StringComparison.OrdinalIgnoreCase) ? "TokyoWarningBrush" :
            "TokyoErrorBrush");

        var answerMessage = new ChatMessageViewModel(string.Equals(workflowState.Status, "done", StringComparison.OrdinalIgnoreCase) ? "ai" : "error", summaryText);
        answerMessage.MarkAnswerPhase();
        answerMessage.UpdateStatus(
            string.Equals(workflowState.Status, "done", StringComparison.OrdinalIgnoreCase) ? "Done" :
            string.Equals(workflowState.Status, "stopped", StringComparison.OrdinalIgnoreCase) ? "Stopped" :
            "Error",
            string.Equals(workflowState.Status, "done", StringComparison.OrdinalIgnoreCase) ? "TokyoSuccessBrush" :
            string.Equals(workflowState.Status, "stopped", StringComparison.OrdinalIgnoreCase) ? "TokyoWarningBrush" :
            "TokyoErrorBrush");
        answerMessage.SetArtifacts(workflowArtifacts);
        Messages.Add(answerMessage);

        GenerationStatusText = string.Equals(workflowState.Status, "done", StringComparison.OrdinalIgnoreCase)
            ? "Done"
            : string.Equals(workflowState.Status, "stopped", StringComparison.OrdinalIgnoreCase)
                ? "Stopped"
                : "Error";
        IsGenerating = false;
        IsThinking = false;

        UpdateRun(runId, state =>
        {
            state.Status = workflowState.Status;
            state.CompletedAtUtc = workflowState.CompletedAtUtc;
            state.OutputPreview = BuildPreview(summaryText);
            state.Error = workflowState.Error;
            state.ArtifactOutputs = workflowArtifacts.Select(artifact => artifact.Path).ToArray();
            state.LiveThinkingText = feedMessage.ExecutionFeedTranscript;
            state.LiveAnswerText = summaryText;
            state.TranscriptMessageCount = Messages.Count;
            state.LastStreamUpdateUtc = DateTimeOffset.UtcNow;
        });

        ChatView.RequestTranscriptScrollToBottom();
        PersistActiveWorkspaceSession();
        RemoveRunSignal(runId);
    }

    private ChatMessageViewModel CreateExecutionFeedMessage(string status, string brushResourceKey)
    {
        var message = new ChatMessageViewModel("ai", string.Empty);
        message.MarkExecutionFeed();
        message.UpdateStatus(status, brushResourceKey);
        return message;
    }

    private void AppendSelectedModelFeedEntry(ChatMessageViewModel feedMessage, string selectedModelLabel)
    {
        if (!string.IsNullOrWhiteSpace(selectedModelLabel))
        {
            AppendFeedEntry(feedMessage, ChatExecutionFeedSection.Model, "model", $"Selected model: {selectedModelLabel}.");
        }
    }

    private void AppendWorkflowFeedDiff(ChatMessageViewModel feedMessage, WorkflowFeedTracker tracker, WorkflowRunState snapshot)
    {
        if (!string.Equals(tracker.LastRunStatus, snapshot.Status, StringComparison.OrdinalIgnoreCase))
        {
            AppendFeedEntry(feedMessage, ChatExecutionFeedSection.Workflow, "workflow", $"Workflow status: {snapshot.Status}.");
            tracker.LastRunStatus = snapshot.Status;
        }

        if (!string.Equals(tracker.LastCurrentStepId, snapshot.CurrentStepId, StringComparison.OrdinalIgnoreCase))
        {
            var currentStep = snapshot.Steps.FirstOrDefault(step => string.Equals(step.StepId, snapshot.CurrentStepId, StringComparison.OrdinalIgnoreCase));
            if (currentStep is not null)
            {
                AppendFeedEntry(feedMessage, ChatExecutionFeedSection.Workflow, "workflow", $"Running step '{currentStep.StepName}'.");
            }

            tracker.LastCurrentStepId = snapshot.CurrentStepId;
        }

        foreach (var step in snapshot.Steps)
        {
            if (!tracker.Steps.TryGetValue(step.StepId, out var stepTracker))
            {
                stepTracker = new WorkflowStepFeedTracker();
                tracker.Steps[step.StepId] = stepTracker;
                AppendFeedEntry(feedMessage, ChatExecutionFeedSection.Workflow, "workflow", $"Step '{step.StepName}' queued.");
            }

            if (!string.Equals(stepTracker.Status, step.Status, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(stepTracker.StatusMessage, step.StatusMessage, StringComparison.OrdinalIgnoreCase))
            {
                AppendFeedEntry(feedMessage, ChatExecutionFeedSection.Workflow, "workflow", $"{step.StepName}: {step.StatusMessage}.");
                stepTracker.Status = step.Status;
                stepTracker.StatusMessage = step.StatusMessage;
            }

            while (stepTracker.LogCount < step.Logs.Count)
            {
                var rawLog = step.Logs[stepTracker.LogCount];
                var section = step.Type.Equals("tool_call", StringComparison.OrdinalIgnoreCase)
                    ? ChatExecutionFeedSection.Workflow
                    : ResolveFeedSection(rawLog, "log", string.Empty, string.Empty);
                var logText = step.Type.Equals("tool_call", StringComparison.OrdinalIgnoreCase)
                    ? $"{step.StepName}: {rawLog}"
                    : $"{step.StepName}: {rawLog}";
                AppendFeedEntry(feedMessage, section, "log", logText);
                stepTracker.LogCount++;
            }

            while (stepTracker.ArtifactCount < step.ArtifactOutputs.Count)
            {
                var artifact = step.ArtifactOutputs[stepTracker.ArtifactCount];
                AppendArtifactFeedEntry(feedMessage, artifact, "info", $"{step.StepName}: ");
                stepTracker.ArtifactCount++;
            }

            if (!string.IsNullOrWhiteSpace(step.Error) && !string.Equals(stepTracker.Error, step.Error, StringComparison.Ordinal))
            {
                AppendFeedEntry(feedMessage, ChatExecutionFeedSection.Workflow, "error", $"{step.StepName}: {step.Error}", "error");
                stepTracker.Error = step.Error;
            }

            if (!string.IsNullOrWhiteSpace(step.FinalText) && !string.Equals(stepTracker.FinalText, step.FinalText, StringComparison.Ordinal))
            {
                AppendFeedEntry(feedMessage, ChatExecutionFeedSection.Workflow, "final_marker", $"{step.StepName}: output ready.", "success");
                stepTracker.FinalText = step.FinalText;
            }
        }

        if (!string.IsNullOrWhiteSpace(snapshot.Error) && !string.Equals(tracker.LastError, snapshot.Error, StringComparison.Ordinal))
        {
            AppendFeedEntry(feedMessage, ChatExecutionFeedSection.Workflow, "error", snapshot.Error, "error");
            tracker.LastError = snapshot.Error;
        }
    }

    private void AppendStreamingFeedText(
        ChatMessageViewModel feedMessage,
        ChatExecutionFeedSection section,
        string kind,
        string text,
        string level,
        string linePrefix)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        feedMessage.AppendStreamingFeedText(section, kind, text, linePrefix, level);
    }

    private void AppendWrapperStreamFeedEvent(
        ChatMessageViewModel feedMessage,
        WrapperRunStreamEvent streamEvent,
        string kind,
        string text,
        string fallbackLevel = "info")
    {
        var level = ResolveFeedLevel(streamEvent, fallbackLevel);
        var section = ResolveFeedSection(text, streamEvent.Type, streamEvent.Section, streamEvent.Category);
        if (!string.IsNullOrWhiteSpace(streamEvent.ChainId))
        {
            feedMessage.AppendChainedFeedEvent(streamEvent, section, kind, text, level);
            return;
        }

        if (streamEvent.IsTokenStream)
        {
            AppendStreamingFeedText(feedMessage, section, kind, text, level, string.IsNullOrWhiteSpace(streamEvent.Title) ? section.ToString() : streamEvent.Title);
            return;
        }

        AppendFeedEntry(feedMessage, section, kind, text, level);
    }

    private void AppendFeedEntry(
        ChatMessageViewModel feedMessage,
        ChatExecutionFeedSection section,
        string kind,
        string text,
        string level = "info")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        feedMessage.AppendFeedEntry(new ChatExecutionFeedEntryViewModel
        {
            Section = section,
            Kind = kind,
            Text = text,
            Level = level,
            Timestamp = DateTimeOffset.UtcNow
        });
    }

    private void AppendArtifactFeedEntry(ChatMessageViewModel feedMessage, WrapperArtifact artifact, string level, string prefix = "")
    {
        var label = string.IsNullOrWhiteSpace(artifact.Label) ? Path.GetFileName(artifact.Path) : artifact.Label;
        feedMessage.AppendFeedEntry(new ChatExecutionFeedEntryViewModel
        {
            Section = ChatExecutionFeedSection.Artifacts,
            Kind = "artifact",
            Text = $"{prefix}Artifact created.",
            ArtifactLabel = string.Concat(prefix, label),
            ArtifactPath = artifact.Path,
            Level = level,
            Timestamp = DateTimeOffset.UtcNow
        });
    }

    private static ChatExecutionFeedSection ResolveFeedSection(string text, string type, string? sectionHint, string? categoryHint) =>
        ChatExecutionFeedClassifier.InferSection(text, type, sectionHint, categoryHint);

    private static string ResolveFeedLevel(WrapperRunStreamEvent streamEvent, string fallback = "info")
    {
        if (!string.IsNullOrWhiteSpace(streamEvent.Level))
        {
            return streamEvent.Level;
        }

        return streamEvent.Type switch
        {
            "error" => "error",
            "final" => "success",
            _ => fallback
        };
    }

    private static string ResolveStatusBrush(WrapperRunStreamEvent streamEvent, string fallback)
    {
        var level = ResolveFeedLevel(streamEvent);
        return level switch
        {
            "error" => "TokyoErrorBrush",
            "success" => "TokyoSuccessBrush",
            "warning" => "TokyoWarningBrush",
            _ => fallback
        };
    }

    private bool TryQueueRun(string? text, bool clearInput, out string runId, out string error) =>
        TryQueueRun(text, clearInput, forcedArtifactInputs: null, out runId, out error);

    private bool TryQueueRun(string? text, bool clearInput, IReadOnlyList<string>? forcedArtifactInputs, out string runId, out string error)
    {
        runId = string.Empty;
        error = string.Empty;

        var normalized = text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            error = "Chat input is empty.";
            return false;
        }

        var draft = ParseComposerDraft(normalized);
        if (forcedArtifactInputs is { Count: > 0 })
        {
            var normalizedInputs = forcedArtifactInputs
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            draft.AttachmentPaths.AddRange(normalizedInputs);
            draft.AttachmentText = string.Join(Environment.NewLine, normalizedInputs);
        }
        ApplyDraftSelection(draft);

        if (draft.ExecutionKind == ChatExecutionKind.Direct)
        {
            var directPlan = ResolveCurrentDirectChatPlan();
            if (directPlan is null || !directPlan.IsRunnable)
            {
                error = directPlan?.FailureReason ?? "Load and activate a model in Models before using direct chat.";
                AppendDirectChatErrorMessage(error);
                _services.Logs.Append("warn", "chat", $"Direct run blocked before queueing: {error}", "direct");
                return false;
            }
        }

        runId = $"chatrun-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..44];
        LatestRunId = runId;

        var selectedInstanceId = SelectedModel?.InstanceId ?? string.Empty;
        var selectedModelId = SelectedModel?.ModelId ?? string.Empty;
        var selectedModelLabel = SelectedModel?.DisplayName ?? "Automatic (best available model)";
        var selectedWorkflowId = SelectedWorkflow?.Id ?? string.Empty;
        var selectedWorkflowName = SelectedWorkflow?.Name ?? string.Empty;
        var selectedWrapperId = SelectedWrapper?.Id ?? string.Empty;
        var selectedWrapperName = SelectedWrapper?.Name ?? string.Empty;

        var queueItem = new ChatQueueItemViewModel
        {
            RunId = runId,
            Status = "queued",
            StatusText = "Queued",
            Title = string.IsNullOrWhiteSpace(draft.WorkflowName)
                ? (draft.ExecutionKind == ChatExecutionKind.Workflow
                    ? "Workflow run"
                    : draft.ExecutionKind == ChatExecutionKind.Wrapper
                        ? "Wrapper run"
                        : "Direct chat")
                : draft.WorkflowName,
            PromptText = draft.BodyText,
            WorkflowName = selectedWorkflowName,
            WrapperName = selectedWrapperName,
            ModelLabel = selectedModelLabel,
            ArtifactSummary = draft.AttachmentPaths.Count == 0 ? "No attachments" : $"{draft.AttachmentPaths.Count} attachment(s)",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Priority = draft.Priority,
            ExecutionKind = draft.ExecutionKind,
            WorkflowId = selectedWorkflowId,
            WrapperId = selectedWrapperId,
            ModelId = selectedModelId,
            SelectedInstanceId = selectedInstanceId,
            AttachmentText = draft.AttachmentText,
            Draft = draft
        };
        foreach (var path in draft.AttachmentPaths)
        {
            queueItem.AttachmentPaths.Add(path);
        }
        foreach (var pair in draft.WorkflowInputs)
        {
            queueItem.WorkflowInputs[pair.Key] = pair.Value;
        }

        lock (_runLock)
        {
            _runs[runId] = new ChatRunState
            {
                RunId = runId,
                Status = "queued",
                Prompt = normalized,
                SelectedInstanceId = selectedInstanceId,
                SelectedModelId = selectedModelId,
                SelectedModelLabel = selectedModelLabel,
                MaxTokens = MaxTokens,
                ExecutionMode = draft.ExecutionKind.ToString().ToLowerInvariant(),
                TemplateId = SelectedTemplate?.Id ?? string.Empty,
                TemplateName = SelectedTemplate?.Name ?? string.Empty,
                WorkflowId = selectedWorkflowId,
                WorkflowName = selectedWorkflowName,
                WrapperId = selectedWrapperId,
                WrapperName = selectedWrapperName,
                ArtifactInputs = draft.AttachmentPaths.ToArray(),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                StreamPhase = string.Empty,
                IsStreaming = false
            };
            _runStartSignals[runId] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        QueueItems.Add(queueItem);
        UpdateQueueStatus();

        if (clearInput)
        {
            InputText = string.Empty;
        }

        _services.Logs.Append("info", "chat", $"Queued {draft.ExecutionKind.ToString().ToLowerInvariant()} run with selected_instance='{selectedInstanceId}', selected_model='{selectedModelId}'.", runId);
        _services.UiAutomation.Highlight(SendButtonElementId);
        _ = ProcessQueueAsync();
        return true;
    }

    private ChatDraft ParseComposerDraft(string normalized)
    {
        var draft = new ChatDraft
        {
            RawText = normalized,
            BodyText = normalized,
            MaxTokens = MaxTokens,
            ModelId = SelectedModel?.ModelId ?? string.Empty,
            ModelLabel = SelectedModel?.DisplayName ?? string.Empty,
            ExecutionKind = ResolveDefaultExecutionKind()
        };

        var tokens = TokenizeDraft(normalized);
        var bodyTokens = new List<string>();
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (token.StartsWith("#", StringComparison.Ordinal) && token.Length > 1)
            {
                ResolveWorkflowSelection(draft, token[1..]);
                draft.ExecutionKind = ChatExecutionKind.Workflow;
                continue;
            }

            if (!token.StartsWith("/", StringComparison.Ordinal) || token.Length == 1)
            {
                bodyTokens.Add(token);
                continue;
            }

            var command = token[1..].ToLowerInvariant();
            var nextValue = index + 1 < tokens.Count ? tokens[index + 1] : string.Empty;
            var consumedValue = false;
            switch (command)
            {
                case "workflow":
                case "wf":
                    if (!string.IsNullOrWhiteSpace(nextValue) && !nextValue.StartsWith("/", StringComparison.Ordinal) && !nextValue.StartsWith("#", StringComparison.Ordinal))
                    {
                        ResolveWorkflowSelection(draft, nextValue);
                        draft.ExecutionKind = ChatExecutionKind.Workflow;
                        consumedValue = true;
                    }
                    break;
                case "agent":
                case "wrapper":
                    if (!string.IsNullOrWhiteSpace(nextValue) && !nextValue.StartsWith("/", StringComparison.Ordinal))
                    {
                        ResolveWrapperSelection(draft, nextValue);
                        draft.ExecutionKind = IsDirectLlmWrapper(SelectedWrapper)
                            ? ChatExecutionKind.Direct
                            : ChatExecutionKind.Wrapper;
                        consumedValue = true;
                    }
                    break;
                case "model":
                    if (!string.IsNullOrWhiteSpace(nextValue) && !nextValue.StartsWith("/", StringComparison.Ordinal))
                    {
                        ResolveModelSelection(draft, nextValue);
                        consumedValue = true;
                    }
                    break;
                case "memory":
                    draft.Warnings.Add("Memory handling is controlled by the selected agent in Agents.");
                    if (!string.IsNullOrWhiteSpace(nextValue) && !nextValue.StartsWith("/", StringComparison.Ordinal))
                    {
                        consumedValue = true;
                    }
                    break;
                case "tokens":
                    draft.Warnings.Add("Token budgets are configured in Models.");
                    if (!string.IsNullOrWhiteSpace(nextValue) && !nextValue.StartsWith("/", StringComparison.Ordinal))
                    {
                        consumedValue = true;
                    }
                    break;
                case "priority":
                    if (!string.IsNullOrWhiteSpace(nextValue))
                    {
                        draft.QueueMode = nextValue.ToLowerInvariant();
                        draft.Priority = draft.QueueMode switch
                        {
                            "high" or "now" => 100,
                            "next" => 50,
                            _ => 0
                        };
                        consumedValue = true;
                    }
                    break;
                case "attach":
                    if (!string.IsNullOrWhiteSpace(nextValue))
                    {
                        draft.AttachmentText = string.IsNullOrWhiteSpace(draft.AttachmentText) ? nextValue : $"{draft.AttachmentText}{Environment.NewLine}{nextValue}";
                        draft.AttachmentPaths.AddRange(ParseArtifactInputs(nextValue));
                        consumedValue = true;
                    }
                    break;
                case "queue":
                    if (!string.IsNullOrWhiteSpace(nextValue))
                    {
                        draft.QueueMode = nextValue.ToLowerInvariant();
                        draft.Priority = draft.QueueMode switch
                        {
                            "now" or "high" => 100,
                            "next" => 50,
                            _ => 0
                        };
                        consumedValue = true;
                    }
                    break;
                case "reset":
                    draft = new ChatDraft
                    {
                        RawText = normalized,
                        BodyText = string.Empty,
                        MaxTokens = MaxTokens,
                        ModelId = SelectedModel?.ModelId ?? string.Empty,
                        ModelLabel = SelectedModel?.DisplayName ?? string.Empty,
                        ExecutionKind = ResolveDefaultExecutionKind()
                    };
                    bodyTokens.Clear();
                    break;
                case "help":
                    draft.Warnings.Add("Use /workflow, /agent, /model, /priority, /attach, and /queue.");
                    break;
                default:
                    bodyTokens.Add(token);
                    break;
            }

            if (consumedValue)
            {
                index++;
            }
        }

        draft.BodyText = string.Join(" ", bodyTokens).Trim();
        if (string.IsNullOrWhiteSpace(draft.BodyText))
        {
            draft.BodyText = normalized;
        }

        if (draft.ExecutionKind == ChatExecutionKind.Workflow && string.IsNullOrWhiteSpace(draft.WorkflowId))
        {
            ResolveWorkflowSelection(draft, normalized.TrimStart('#'));
        }

        return draft;
    }

    private static List<string> TokenizeDraft(string text)
    {
        var matches = Regex.Matches(text, "\"[^\"]*\"|'[^']*'|\\S+");
        return matches.Select(match => match.Value.Trim('"').Trim('\'')).ToList();
    }

    private void ResolveWorkflowSelection(ChatDraft draft, string workflowQuery)
    {
        var workflow = ResolveWorkflow(workflowQuery);
        if (workflow is null)
        {
            if (!string.IsNullOrWhiteSpace(workflowQuery))
            {
                draft.Warnings.Add($"Workflow '{workflowQuery}' was not found.");
            }
            return;
        }

        draft.WorkflowId = workflow.Id;
        draft.WorkflowName = workflow.Name;
        SelectedWorkflow = workflow;
        SelectedWorkflowStatusText = $"Workflow: {workflow.Name}";
    }

    private void ResolveWrapperSelection(ChatDraft draft, string wrapperQuery)
    {
        var wrapper = ResolveWrapper(wrapperQuery);
        if (wrapper is null)
        {
            if (!string.IsNullOrWhiteSpace(wrapperQuery))
            {
                draft.Warnings.Add($"Wrapper '{wrapperQuery}' was not found.");
            }
            return;
        }

        draft.WrapperId = wrapper.Id;
        draft.WrapperName = wrapper.Name;
        SelectedWrapper = wrapper;
    }

    private void ResolveModelSelection(ChatDraft draft, string modelQuery)
    {
        var model = AvailableModels.FirstOrDefault(option =>
            string.Equals(option.InstanceId, modelQuery, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(option.ModelId, modelQuery, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(option.DisplayName, modelQuery, StringComparison.OrdinalIgnoreCase));

        if (model is null)
        {
            draft.Warnings.Add($"Model '{modelQuery}' was not found.");
            return;
        }

        SelectedModel = model;
        draft.ModelId = model.ModelId;
        draft.ModelLabel = model.DisplayName;
    }

    private void ApplyDraftSelection(ChatDraft draft)
    {
        if (!string.IsNullOrWhiteSpace(draft.ModelId))
        {
            ResolveModelSelection(draft, draft.ModelId);
        }

        if (!string.IsNullOrWhiteSpace(draft.WorkflowId))
        {
            ResolveWorkflowSelection(draft, draft.WorkflowId);
        }
        else
        {
            SelectedWorkflow = null;
        }

        if (!string.IsNullOrWhiteSpace(draft.WrapperId))
        {
            ResolveWrapperSelection(draft, draft.WrapperId);
        }

        if (draft.AttachmentPaths.Count > 0)
        {
            ArtifactInputsText = string.Join(Environment.NewLine, draft.AttachmentPaths);
        }
        else
        {
            ArtifactInputsText = string.Empty;
        }

        SelectedExecutionMode = draft.ExecutionKind == ChatExecutionKind.Workflow
            ? "workflow"
            : draft.ExecutionKind == ChatExecutionKind.Wrapper
                ? "wrapper"
                : "direct";

        UpdateWorkflowSelectionState();
        UpdateWrapperSelectionState();
    }

    private IReadOnlyDictionary<string, string> BuildWorkflowInputValues(WorkflowDefinition workflow, string userText, ChatDraft draft)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var input in workflow.Inputs)
        {
            var value = string.Empty;
            if (draft.WorkflowInputs.TryGetValue(input.Key, out var mappedValue))
            {
                value = mappedValue;
            }
            else if (!string.IsNullOrWhiteSpace(input.DefaultValue))
            {
                value = input.DefaultValue;
            }
            else if (string.Equals(input.Kind, "files", StringComparison.OrdinalIgnoreCase) && draft.AttachmentPaths.Count > 0)
            {
                value = string.Join(Environment.NewLine, draft.AttachmentPaths);
            }
            else if (string.Equals(input.Kind, "text", StringComparison.OrdinalIgnoreCase))
            {
                value = userText;
            }

            values[input.Key] = value;
        }

        if (!values.ContainsKey("prompt"))
        {
            values["prompt"] = userText;
        }

        if (draft.AttachmentPaths.Count > 0 && !values.ContainsKey("artifacts"))
        {
            values["artifacts"] = string.Join(Environment.NewLine, draft.AttachmentPaths);
        }

        return values;
    }

    private static string BuildWorkflowSummary(WorkflowRunState workflowState)
    {
        var parts = new List<string>
        {
            $"{workflowState.WorkflowName} completed with status {workflowState.Status}."
        };

        if (workflowState.Steps.Count > 0)
        {
            parts.Add($"Steps: {workflowState.Steps.Count}.");
        }

        var notableSteps = workflowState.Steps
            .Where(step => !string.Equals(step.Status, "skipped", StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .Select(step => $"{step.StepName}: {step.StatusMessage}");
        parts.AddRange(notableSteps);

        var artifacts = workflowState.Steps
            .SelectMany(step => step.ArtifactOutputs)
            .Select(artifact => artifact.Path)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
        if (artifacts.Count > 0)
        {
            parts.Add("Artifacts:");
            parts.AddRange(artifacts.Select(path => $"- {path}"));
        }

        if (!string.IsNullOrWhiteSpace(workflowState.Error))
        {
            parts.Add($"Error: {workflowState.Error}");
        }

        return string.Join(Environment.NewLine, parts);
    }

    private WorkflowLibraryItemViewModel? ResolveWorkflow(string workflowQuery)
    {
        if (string.IsNullOrWhiteSpace(workflowQuery))
        {
            return SelectedWorkflow;
        }

        return AvailableWorkflows.FirstOrDefault(workflow =>
            string.Equals(workflow.Id, workflowQuery, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(workflow.Name, workflowQuery, StringComparison.OrdinalIgnoreCase) ||
            workflow.Description.Contains(workflowQuery, StringComparison.OrdinalIgnoreCase));
    }

    private ChatExecutionKind ResolveDefaultExecutionKind()
    {
        return IsDirectLlmWrapper(ResolveEffectiveWrapper())
            ? ChatExecutionKind.Direct
            : ChatExecutionKind.Wrapper;
    }

    private WrapperDefinition? ResolveWrapper(string wrapperQuery)
    {
        if (string.IsNullOrWhiteSpace(wrapperQuery))
        {
            return SelectedAgent;
        }

        return AvailableAgents.FirstOrDefault(wrapper =>
            string.Equals(wrapper.Id, wrapperQuery, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(wrapper.Name, wrapperQuery, StringComparison.OrdinalIgnoreCase));
    }

    private async Task ProcessQueueAsync()
    {
        if (_queuePumpActive)
        {
            return;
        }

        await _queuePumpLock.WaitAsync();
        try
        {
            if (_queuePumpActive)
            {
                return;
            }

            _queuePumpActive = true;
            while (true)
            {
                var next = QueueItems
                    .Where(item => item.IsQueued)
                    .OrderByDescending(item => item.Priority)
                    .ThenBy(item => item.CreatedAtUtc)
                    .FirstOrDefault();
                if (next is null)
                {
                    break;
                }

                await ExecuteQueuedItemAsync(next);
            }
        }
        finally
        {
            _queuePumpActive = false;
            _queuePumpLock.Release();
            UpdateQueueStatus();
        }
    }

    private async Task ExecuteQueuedItemAsync(ChatQueueItemViewModel item)
    {
        item.IsActive = true;
        item.Status = "running";
        item.StatusText = "Running";
        item.StartedAtUtc = DateTimeOffset.UtcNow;
        UpdateQueueStatus();

        switch (item.ExecutionKind)
        {
            case ChatExecutionKind.Workflow:
                await ExecuteWorkflowRunAsync(item.RunId, item.Draft.BodyText, item.SelectedInstanceId, item.ModelId, item.ModelLabel, item.Draft);
                break;
            case ChatExecutionKind.Wrapper:
                await ExecuteWrapperRunAsync(item.RunId, item.Draft.BodyText, item.ModelId, item.ModelLabel);
                break;
            default:
                await ExecuteDirectRunAsync(item.RunId, item.Draft.BodyText, item.SelectedInstanceId, item.ModelId, item.ModelLabel);
                break;
        }

        var run = GetRun(item.RunId);
        item.Status = run?.Status ?? "done";
        item.StatusText = string.Equals(item.Status, "done", StringComparison.OrdinalIgnoreCase)
            ? "Done"
            : string.Equals(item.Status, "stopped", StringComparison.OrdinalIgnoreCase)
                ? "Stopped"
                : "Error";
        item.CompletedAtUtc = run?.CompletedAtUtc ?? DateTimeOffset.UtcNow;
        item.ErrorText = run?.Error ?? string.Empty;
        item.IsActive = false;
        UpdateQueueStatus();
    }

    private void UpdateQueueStatus()
    {
        var active = QueueItems.FirstOrDefault(item => item.IsRunning);
        var queuedCount = QueueItems.Count(item => item.IsQueued);
        QueueStatusText = active is null
            ? queuedCount == 0
                ? "Queue empty"
                : $"{queuedCount} queued"
            : $"Running {active.Title}";
        if (active is not null)
        {
            GenerationStatusText = active.StatusText;
            return;
        }

        if (ShouldResetGenerationStatusToIdle(GenerationStatusText))
        {
            GenerationStatusText = "Idle";
        }
    }

    private async Task WaitForRunToStartAsync(string runId)
    {
        TaskCompletionSource<bool>? signal;
        lock (_runLock)
        {
            if (_runs.TryGetValue(runId, out var run) &&
                !string.Equals(run.Status, "queued", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!_runStartSignals.TryGetValue(runId, out signal))
            {
                signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _runStartSignals[runId] = signal;
            }
        }

        await signal!.Task;
    }

    private void RefreshLatestRunReference()
    {
        string latestRunId;
        lock (_runLock)
        {
            latestRunId = _runs.Values
                .OrderByDescending(run => run.CreatedAtUtc)
                .Select(run => run.RunId)
                .FirstOrDefault()
                ?? string.Empty;
        }

        LatestRunId = latestRunId;
    }

    private void UpdateRun(string runId, Action<ChatRunState> updater)
    {
        lock (_runLock)
        {
            if (_runs.TryGetValue(runId, out var run))
            {
                updater(run);
            }
        }
    }

    private void SignalRunStarted(string runId)
    {
        TaskCompletionSource<bool>? signal;
        lock (_runLock)
        {
            _runStartSignals.TryGetValue(runId, out signal);
        }

        signal?.TrySetResult(true);
    }

    private void RemoveRunSignal(string runId)
    {
        lock (_runLock)
        {
            _runStartSignals.Remove(runId);
        }
    }

    private void RefreshAvailableModels()
    {
        var selectedInstanceId = SelectedModel?.InstanceId ?? string.Empty;
        var selectedModelId = SelectedModel?.ModelId ?? string.Empty;
        var hadExplicitSelection = !string.IsNullOrWhiteSpace(selectedInstanceId);
        var autoInstance = ResolveAutoChatInstance();
        AvailableModels.Clear();
        AvailableModels.Add(new ChatModelOptionViewModel(
            string.Empty,
            string.Empty,
            "Auto",
            autoInstance?.MaxTokens ?? 1200,
            "auto",
            autoInstance?.RuntimeBackend ?? "automatic",
            autoInstance is not null,
            autoInstance is null ? "Load and activate a model in Models first." : string.Empty));

        foreach (var instance in _services.Library.GetChatAvailableInstances())
        {
            AvailableModels.Add(new ChatModelOptionViewModel(
                instance.InstanceId,
                instance.ModelId,
                instance.DisplayName,
                instance.MaxTokens,
                "instance",
                instance.RuntimeBackend,
                true,
                string.Empty));
        }

        SelectedModel = AvailableModels
            .FirstOrDefault(option => hadExplicitSelection && string.Equals(option.InstanceId, selectedInstanceId, StringComparison.OrdinalIgnoreCase))
            ?? AvailableModels.FirstOrDefault(option =>
                hadExplicitSelection &&
                string.Equals(option.ModelId, selectedModelId, StringComparison.OrdinalIgnoreCase))
            ?? AvailableModels.FirstOrDefault();
        ApplyCurrentAgentDefaults();
        UpdateSelectionState();
    }

    private void RefreshAvailableWorkflows()
    {
        var selectedWorkflowId = SelectedWorkflow?.Id ?? string.Empty;
        AvailableWorkflows.Clear();
        foreach (var workflow in _services.Workflows.GetWorkflows().OrderBy(workflow => workflow.Name, StringComparer.OrdinalIgnoreCase))
        {
            AvailableWorkflows.Add(new WorkflowLibraryItemViewModel(workflow));
        }

        SelectedWorkflow = AvailableWorkflows.FirstOrDefault(workflow => string.Equals(workflow.Id, selectedWorkflowId, StringComparison.OrdinalIgnoreCase));
        UpdateWorkflowSelectionState();
    }

    private void RefreshAvailableTemplates()
    {
        var selectedTemplateId = SelectedTemplate?.Id ?? string.Empty;
        AvailableTemplates.Clear();
        foreach (var template in _services.Templates.GetTemplates().Where(template => template.IsValid))
        {
            AvailableTemplates.Add(template);
        }

        SelectedTemplate = AvailableTemplates.FirstOrDefault(template => string.Equals(template.Id, selectedTemplateId, StringComparison.OrdinalIgnoreCase))
            ?? ResolveTemplateForAgent(SelectedAgent)
            ?? AvailableTemplates.FirstOrDefault(template => string.Equals(template.Id, "direct_chat", StringComparison.OrdinalIgnoreCase))
            ?? AvailableTemplates.FirstOrDefault();
        RefreshAvailableWrappers();
    }

    private void RefreshOpenWorkspaces()
    {
        var selectedWorkspaceId = _services.Workspaces.GetActiveWorkspace()?.Id ?? _selectedWorkspaceId;
        var selectedNodePath = SelectedWorkspaceNode?.FullPath ?? string.Empty;
        var expansionState = CaptureExpansionState();
        var workspaces = _services.Workspaces.GetWorkspaces();
        ExplorerRoots.Clear();
        foreach (var workspace in workspaces)
        {
            var root = CreateWorkspaceRootNode(workspace, expansionState);
            ExplorerRoots.Add(root);
            if (root.IsExpanded)
            {
                LoadNodeChildren(root, forceReload: false, expansionState);
            }
        }

        _selectedWorkspaceId = string.IsNullOrWhiteSpace(selectedWorkspaceId)
            ? ExplorerRoots.FirstOrDefault()?.WorkspaceId ?? string.Empty
            : selectedWorkspaceId;
        RebuildVisibleExplorerNodes();
        if (!string.IsNullOrWhiteSpace(selectedNodePath) &&
            RestoreSelectedExplorerNode(_selectedWorkspaceId, selectedNodePath, persistSelection: false, fallbackToRoot: true))
        {
        }
        else if (!string.IsNullOrWhiteSpace(_selectedWorkspaceId))
        {
            SelectWorkspaceRoot(_selectedWorkspaceId, persistSelection: false);
        }
        else
        {
            SetSelectedExplorerNodeInternal(null, persistSelection: false);
        }

        OnPropertyChanged(nameof(OpenWorkspaces));
        OnPropertyChanged(nameof(ActiveWorkspaceRootDirectory));
        OnPropertyChanged(nameof(ActiveWorkspaceName));
        OnPropertyChanged(nameof(ActiveWorkspacePath));
        OnPropertyChanged(nameof(CanSaveSandbox));
        OnPropertyChanged(nameof(CanDeleteSelectedSandbox));
    }

    private void SelectWorkspaceById(string? workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return;
        }

        if (!_services.Workspaces.SelectWorkspace(workspaceId))
        {
            return;
        }

        _selectedWorkspaceId = workspaceId;
        RefreshOpenWorkspaces();
    }

    private void LoadWorkspaceSession(string? workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return;
        }

        _loadingWorkspaceSession = true;
        try
        {
            var bundle = _services.Workspaces.LoadSession(workspaceId);
            RestoreWorkspaceSession(bundle);
        }
        finally
        {
            _loadingWorkspaceSession = false;
        }
    }

    private void RestoreWorkspaceSession(WorkspaceSessionBundle bundle)
    {
        _loadedWorkspaceId = bundle.WorkspaceId;
        Messages.Clear();
        InputText = bundle.InputText ?? string.Empty;
        ArtifactInputsText = bundle.ArtifactInputsText ?? string.Empty;
        ShowArtifactInputBox = !string.IsNullOrWhiteSpace(ArtifactInputsText);
        SessionId = bundle.SessionId;
        WorkspaceStatusText = string.IsNullOrWhiteSpace(bundle.RootDirectory)
            ? string.Empty
            : $"Workspace root: {bundle.RootDirectory}";

        foreach (var message in bundle.Messages.Select(CreateMessageFromRecord))
        {
            Messages.Add(message);
        }

        if (Messages.Count == 0)
        {
            AddWelcomeMessage();
        }

        if (!string.IsNullOrWhiteSpace(bundle.AgentId))
        {
            var workspaceWrapper = AvailableWrappers.FirstOrDefault(wrapper =>
                string.Equals(wrapper.Id, bundle.AgentId, StringComparison.OrdinalIgnoreCase));
            if (workspaceWrapper is not null)
            {
                SelectedWrapper = workspaceWrapper;
            }
        }

        if (!string.IsNullOrWhiteSpace(bundle.ModelId))
        {
            var workspaceModel = AvailableModels.FirstOrDefault(model =>
                string.Equals(model.InstanceId, bundle.ModelId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(model.ModelId, bundle.ModelId, StringComparison.OrdinalIgnoreCase));
            if (workspaceModel is not null)
            {
                SelectedModel = workspaceModel;
            }
        }

        SetSelectedExplorerNodeInternal(null, persistSelection: false);
        if (!string.IsNullOrWhiteSpace(bundle.SelectedFilePath))
        {
            RestoreSelectedExplorerNode(bundle.WorkspaceId, bundle.SelectedFilePath, persistSelection: false, fallbackToRoot: true);
        }
        else if (!string.IsNullOrWhiteSpace(bundle.SelectedFolderPath))
        {
            RestoreSelectedExplorerNode(bundle.WorkspaceId, bundle.SelectedFolderPath, persistSelection: false, fallbackToRoot: true);
        }
        else
        {
            SelectWorkspaceRoot(bundle.WorkspaceId, persistSelection: false);
        }

        lock (_runLock)
        {
            _runs.Clear();
        }

        QueueItems.Clear();
        QueueStatusText = "Queue empty";
        ChatView.RequestTranscriptScrollToBottom();
    }

    private ExplorerTreeNodeViewModel CreateWorkspaceRootNode(WorkspaceDefinition workspace, IReadOnlyDictionary<string, bool> expansionState)
    {
        return new ExplorerTreeNodeViewModel(
            ExplorerTreeNodeKind.WorkspaceRoot,
            workspace.Id,
            workspace.Name,
            workspace.Kind,
            workspace.RootDirectory,
            string.Empty,
            workspace.Name,
            hasChildren: true,
            depth: 0,
            isPinned: workspace.Pinned)
        {
            IsExpanded = expansionState.TryGetValue($"root::{workspace.Id}", out var expanded) && expanded
        };
    }

    private static ExplorerTreeNodeViewModel CreateExplorerNode(
        ExplorerTreeNodeViewModel parent,
        WorkspaceFileNode node,
        IReadOnlyDictionary<string, bool> expansionState)
    {
        var child = new ExplorerTreeNodeViewModel(
            node.IsDirectory ? ExplorerTreeNodeKind.Directory : ExplorerTreeNodeKind.File,
            parent.WorkspaceId,
            parent.WorkspaceName,
            parent.WorkspaceKind,
            node.FullPath,
            node.RelativePath,
            string.IsNullOrWhiteSpace(node.Name) ? node.FullPath : node.Name,
            node.HasChildren,
            parent.Depth + 1,
            parent.IsPinned);
        child.IsExpanded = child.IsDirectory &&
            expansionState.TryGetValue(child.NodeKey, out var expanded) &&
            expanded;
        return child;
    }

    private void LoadNodeChildren(
        ExplorerTreeNodeViewModel node,
        bool forceReload,
        IReadOnlyDictionary<string, bool>? expansionState = null)
    {
        if (node is null || !node.IsDirectory || (!forceReload && node.ChildrenLoaded))
        {
            return;
        }

        expansionState ??= CaptureExpansionState();
        node.Children.Clear();
        var children = node.IsWorkspaceRoot
            ? _services.Workspaces.GetRootNodes(node.WorkspaceId)
            : _services.Workspaces.GetChildren(node.WorkspaceId, node.FullPath);
        foreach (var child in children.Select(child => CreateExplorerNode(node, child, expansionState)))
        {
            node.Children.Add(child);
            if (child.IsExpanded)
            {
                LoadNodeChildren(child, forceReload: false, expansionState);
            }
        }

        node.ChildrenLoaded = true;
    }

    private Dictionary<string, bool> CaptureExpansionState()
    {
        var state = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in ExplorerRoots)
        {
            foreach (var node in root.DescendantsAndSelf().Where(candidate => candidate.HasChildren))
            {
                state[node.NodeKey] = node.IsExpanded;
            }
        }

        return state;
    }

    private bool RestoreSelectedExplorerNode(string workspaceId, string fullPath, bool persistSelection, bool fallbackToRoot)
    {
        var root = FindExplorerRoot(workspaceId);
        if (root is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(fullPath) ||
            string.Equals(Path.GetFullPath(fullPath), root.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            SelectWorkspaceRoot(workspaceId, persistSelection);
            return true;
        }

        var relativePath = _services.Workspaces.ResolveWorkspaceRelativePath(workspaceId, fullPath);
        if (relativePath is null)
        {
            return fallbackToRoot && SelectWorkspaceRoot(workspaceId, persistSelection);
        }

        root.IsExpanded = true;
        LoadNodeChildren(root, forceReload: false);
        ExplorerTreeNodeViewModel currentNode = root;
        var currentPath = root.FullPath;
        foreach (var segment in relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            var nextNode = currentNode.Children.FirstOrDefault(node =>
                string.Equals(node.FullPath, currentPath, StringComparison.OrdinalIgnoreCase));
            if (nextNode is null)
            {
                return fallbackToRoot && SelectWorkspaceRoot(workspaceId, persistSelection);
            }

            currentNode = nextNode;
            if (currentNode.IsDirectory)
            {
                currentNode.IsExpanded = true;
                LoadNodeChildren(currentNode, forceReload: false);
            }
        }

        RebuildVisibleExplorerNodes();
        SetSelectedExplorerNodeInternal(currentNode, persistSelection);
        return true;
    }

    private bool SelectWorkspaceRoot(string workspaceId, bool persistSelection)
    {
        var root = FindExplorerRoot(workspaceId);
        if (root is null)
        {
            return false;
        }

        SetSelectedExplorerNodeInternal(root, persistSelection);
        return true;
    }

    private ExplorerTreeNodeViewModel? FindExplorerRoot(string workspaceId)
    {
        return ExplorerRoots.FirstOrDefault(root =>
            string.Equals(root.WorkspaceId, workspaceId, StringComparison.OrdinalIgnoreCase));
    }

    private void RebuildVisibleExplorerNodes()
    {
        VisibleExplorerNodes.Clear();
        foreach (var root in ExplorerRoots)
        {
            AddVisibleNode(root);
        }
    }

    private void AddVisibleNode(ExplorerTreeNodeViewModel node)
    {
        VisibleExplorerNodes.Add(node);
        if (!node.IsDirectory || !node.IsExpanded)
        {
            return;
        }

        if (!node.ChildrenLoaded)
        {
            LoadNodeChildren(node, forceReload: false);
        }

        foreach (var child in node.Children)
        {
            AddVisibleNode(child);
        }
    }

    private void SetSelectedExplorerNodeInternal(ExplorerTreeNodeViewModel? node, bool persistSelection)
    {
        _syncingExplorerSelection = true;
        try
        {
            SelectedExplorerNode = node;
        }
        finally
        {
            _syncingExplorerSelection = false;
        }

        foreach (var root in ExplorerRoots)
        {
            foreach (var candidate in root.DescendantsAndSelf())
            {
                candidate.IsSelected = ReferenceEquals(candidate, node);
            }
        }

        _selectedWorkspaceId = node?.WorkspaceId ?? _services.Workspaces.GetActiveWorkspace()?.Id ?? string.Empty;
        OnPropertyChanged(nameof(SelectedWorkspace));
        OnPropertyChanged(nameof(SelectedWorkspaceNode));
        OnPropertyChanged(nameof(ActiveWorkspaceRootDirectory));
        OnPropertyChanged(nameof(ActiveWorkspaceName));
        OnPropertyChanged(nameof(ActiveWorkspacePath));
        OnPropertyChanged(nameof(SelectedWorkspaceTargetPath));
        OnPropertyChanged(nameof(SelectedWorkspaceTargetStatusText));
        OnPropertyChanged(nameof(CanSaveSandbox));
        OnPropertyChanged(nameof(CanDeleteSelectedSandbox));

        if (persistSelection && !_loadingWorkspaceSession)
        {
            PersistActiveWorkspaceSession();
        }
    }

    private static bool IsNodeWithinSubtree(ExplorerTreeNodeViewModel candidate, ExplorerTreeNodeViewModel root)
    {
        if (!string.Equals(candidate.WorkspaceId, root.WorkspaceId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(candidate.FullPath, root.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalizedRoot = root.FullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedCandidate = candidate.FullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var prefix = normalizedRoot + Path.DirectorySeparatorChar;
        return normalizedCandidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private void PersistActiveWorkspaceSession()
    {
        if (_loadingWorkspaceSession || _persistingWorkspaceSession || string.IsNullOrWhiteSpace(_loadedWorkspaceId))
        {
            return;
        }

        try
        {
            _persistingWorkspaceSession = true;
            var workspace = _services.Workspaces.FindWorkspace(_loadedWorkspaceId);
            if (workspace is null)
            {
                return;
            }

            _services.Workspaces.SaveSession(new WorkspaceSessionBundle
            {
                WorkspaceId = workspace.Id,
                WorkspaceName = workspace.Name,
                RootDirectory = workspace.RootDirectory,
                SessionId = SessionId,
                AgentId = SelectedWrapper?.Id ?? string.Empty,
                ModelId = SelectedModel?.InstanceId ?? SelectedModel?.ModelId ?? string.Empty,
                ArtifactInputsText = ArtifactInputsText,
                InputText = InputText,
                SelectedFilePath = SelectedWorkspaceNode is not null && !SelectedWorkspaceNode.IsDirectory ? SelectedWorkspaceNode.FullPath : string.Empty,
                SelectedFolderPath = SelectedWorkspaceNode is not null && SelectedWorkspaceNode.IsDirectory ? SelectedWorkspaceNode.FullPath : string.Empty,
                Messages = Messages.Select(CreateMessageRecord).ToList()
            });
        }
        finally
        {
            _persistingWorkspaceSession = false;
        }
    }

    private void RefreshMessageTransitions()
    {
        ChatMessageViewModel? previous = null;
        foreach (var message in Messages)
        {
            message.ShowUserToAiDivider = previous is not null &&
                string.Equals(previous.Role, "user", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase);
            previous = message;
        }
    }

    private void RefreshAvailableWrappers()
    {
        var selectedWrapperId = SelectedAgent?.Id ?? string.Empty;
        var wrappers = _services.Wrappers.GetWrappers()
            .Where(wrapper => wrapper.SupportsChat && (
                string.Equals(wrapper.Id, "nexusglmharness", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(wrapper.Id, "nexusagent", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(wrapper.Id, "direct_llm", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(wrapper => GetAgentSortOrder(wrapper.Id))
            .ThenBy(wrapper => wrapper.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _refreshingWrappers = true;
        try
        {
            AvailableWrappers.Clear();
            foreach (var wrapper in wrappers)
            {
                AvailableWrappers.Add(wrapper);
            }

            var preserved = AvailableWrappers.FirstOrDefault(wrapper => string.Equals(wrapper.Id, selectedWrapperId, StringComparison.OrdinalIgnoreCase));
            var fallback = GetDefaultAgent(AvailableWrappers)
                ?? AvailableWrappers.FirstOrDefault();
            var nextSelection = preserved ?? fallback;
            var previousSelectionId = SelectedWrapper?.Id ?? string.Empty;

            if (!ReferenceEquals(SelectedWrapper, nextSelection) &&
                (!string.Equals(previousSelectionId, nextSelection?.Id ?? string.Empty, StringComparison.OrdinalIgnoreCase) || nextSelection is not null))
            {
                SelectedWrapper = nextSelection;
            }
        }
        finally
        {
            _refreshingWrappers = false;
        }

        var currentSelectionId = SelectedWrapper?.Id ?? string.Empty;
        if (!string.Equals(selectedWrapperId, currentSelectionId, StringComparison.OrdinalIgnoreCase) &&
            !_applyingAgentSelection)
        {
            ApplyCurrentAgentDefaults();
        }

        UpdateWrapperSelectionState();
    }

    private static string FormatShortInstanceLabel(string instanceId) =>
        string.IsNullOrWhiteSpace(instanceId)
            ? "auto"
            : instanceId.Length > 7 ? instanceId[..7] : instanceId;

    private void SyncAutomationState()
    {
        IsModelDropdownHighlighted = _services.UiAutomation.IsHighlighted(ModelDropdownElementId);
        IsModelDropdownOpen = _services.UiAutomation.IsExpanded(ModelDropdownElementId);
        IsAgentDropdownHighlighted = _services.UiAutomation.IsHighlighted(AgentDropdownElementId);
        IsAgentDropdownOpen = _services.UiAutomation.IsExpanded(AgentDropdownElementId);
        IsAttachButtonHighlighted = _services.UiAutomation.IsHighlighted(AttachButtonElementId);
        IsInputHighlighted = _services.UiAutomation.IsHighlighted(InputElementId);
        IsMaxTokensHighlighted = _services.UiAutomation.IsHighlighted(MaxTokensElementId);
        IsSendButtonHighlighted = _services.UiAutomation.IsHighlighted(SendButtonElementId);
        IsReloadButtonHighlighted = _services.UiAutomation.IsHighlighted(ReloadButtonElementId);
        UpdateSelectionState();
    }

    partial void OnIsModelDropdownOpenChanged(bool value)
    {
        _services.UiAutomation.SetExpanded(ModelDropdownElementId, value);
        if (value)
        {
            _services.UiAutomation.Highlight(ModelDropdownElementId);
        }

        UpdateSelectionState();
    }

    partial void OnIsAgentDropdownOpenChanged(bool value)
    {
        _services.UiAutomation.SetExpanded(AgentDropdownElementId, value);
        if (value)
        {
            _services.UiAutomation.Highlight(AgentDropdownElementId);
        }
    }

    partial void OnSelectedModelChanged(ChatModelOptionViewModel? value)
    {
        if (value is not null && value.DefaultMaxTokens > 0)
        {
            MaxTokens = value.DefaultMaxTokens;
        }
        UpdateSelectionState();
        if (!_loadingWorkspaceSession)
        {
            PersistActiveWorkspaceSession();
        }
    }

    partial void OnSelectedExecutionModeChanged(string value)
    {
        UpdateWorkflowSelectionState();
        UpdateWrapperSelectionState();
    }

    partial void OnSelectedWorkflowChanged(WorkflowLibraryItemViewModel? value)
    {
        UpdateWorkflowSelectionState();
    }

    partial void OnShowArtifactInputBoxChanged(bool value)
    {
        _services.UiAutomation.SetExpanded(AttachButtonElementId, value);
    }

    partial void OnInputTextChanged(string value)
    {
        if (!_loadingWorkspaceSession)
        {
            PersistActiveWorkspaceSession();
        }
    }

    partial void OnArtifactInputsTextChanged(string value)
    {
        if (!_loadingWorkspaceSession)
        {
            PersistActiveWorkspaceSession();
        }
    }

    partial void OnMaxTokensChanged(int value)
    {
        var bounded = Math.Clamp(value, 100, 5000);
        if (bounded != value)
        {
            MaxTokens = bounded;
            return;
        }

        var instanceId = !string.IsNullOrWhiteSpace(SelectedModel?.InstanceId)
            ? SelectedModel.InstanceId
            : ResolveAutoChatInstance()?.InstanceId;
        if (!string.IsNullOrWhiteSpace(instanceId))
        {
            _services.Library.UpdateInstanceTokens(instanceId, bounded);
        }

        if (!_loadingWorkspaceSession)
        {
            PersistActiveWorkspaceSession();
        }

        OnPropertyChanged(nameof(EffectiveMaxTokensStatusText));
    }

    partial void OnSelectedExplorerNodeChanged(ExplorerTreeNodeViewModel? value)
    {
        if (_syncingExplorerSelection)
        {
            return;
        }

        if (value is null)
        {
            if (!string.IsNullOrWhiteSpace(_selectedWorkspaceId))
            {
                SelectWorkspaceRoot(_selectedWorkspaceId, persistSelection: false);
            }

            return;
        }

        if (!string.Equals(SelectedWorkspace?.Id, value.WorkspaceId, StringComparison.OrdinalIgnoreCase))
        {
            ActivateWorkspaceAndSelect(value.WorkspaceId, value.IsWorkspaceRoot ? null : value.FullPath, value.IsWorkspaceRoot);
            return;
        }

        SetSelectedExplorerNodeInternal(value, persistSelection: true);

        if (value is not null && value.IsFile)
        {
            PreviewFileName = value.DisplayName;
            _ = LoadPreviewContentAsync(value.FullPath);
        }
        else
        {
            IsPreviewVisible = false;
        }
    }

    partial void OnSelectedTemplateChanged(TemplateDefinition? value)
    {
        if (!_applyingAgentSelection)
        {
            _applyingTemplateDefaults = true;
            try
            {
                ApplyTemplateDefaults(value);
            }
            finally
            {
                _applyingTemplateDefaults = false;
            }
        }
        UpdateWrapperSelectionState();
        if (!_loadingWorkspaceSession)
        {
            PersistActiveWorkspaceSession();
        }
    }

    partial void OnSelectedWrapperChanged(WrapperDefinition? value)
    {
        OnPropertyChanged(nameof(SelectedAgent));
        ApplyWrapperCapabilities(value);

        if (_refreshingWrappers)
        {
            UpdateWrapperSelectionState();
            return;
        }

        if (!_applyingTemplateDefaults)
        {
            SelectedWorkflow = null;
        }

        if (!_applyingTemplateDefaults)
        {
            ApplyCurrentAgentDefaults();
        }

        UpdateWrapperSelectionState();
        if (!_loadingWorkspaceSession)
        {
            PersistActiveWorkspaceSession();
        }
    }

    private void UpdateSelectionState()
    {
        var directPlan = ResolveCurrentDirectChatPlan();
        HasExplicitModelSelection = !string.IsNullOrWhiteSpace(SelectedModel?.InstanceId);
        ModelDropdownStatusText = IsModelDropdownHighlighted || IsModelDropdownOpen
            ? $"Tool focus: {ModelDropdownElementId}"
            : HasExplicitModelSelection && SelectedModel is not null
                ? $"Selected model: {SelectedModel.DisplayName}"
                : directPlan is not null && directPlan.IsRunnable
                    ? $"Selected model: Auto -> {directPlan.ResolvedModelLabel}"
                    : SelectedModel is not null && !SelectedModel.IsRunnable && !string.IsNullOrWhiteSpace(SelectedModel.RunnableReason)
                        ? $"Selected model: Auto ({SelectedModel.RunnableReason})"
                        : "Selected model: Auto";
        UpdateWorkflowSelectionState();
        UpdateWrapperSelectionState();
    }

    private void UpdateWorkflowSelectionState()
    {
        var workflowName = SelectedWorkflow?.Name ?? "none";
        SelectedWorkflowStatusText = SelectedWorkflow is not null
            ? $"Workflow: {workflowName}"
            : "Workflow: none";
    }

    private void AddWelcomeMessage()
    {
        var welcome = new ChatMessageViewModel("ai", "Hello! I am your NexusCentral assistant. I can help you manage models, run agents, and coordinate workflows. What would you like to do?");
        welcome.UpdateStatus("Ready", "TokyoSuccessBrush");
        Messages.Add(welcome);
    }

    private static WorkspaceChatMessageRecord CreateMessageRecord(ChatMessageViewModel message)
    {
        return new WorkspaceChatMessageRecord
        {
            Role = message.Role,
            Text = message.Text,
            Status = message.Status,
            StatusBrushResourceKey = message.StatusBrushResourceKey,
            ThinkingText = message.ThinkingText,
            IsThinkingExpanded = message.IsThinkingExpanded,
            IsThinkingPhase = message.IsThinkingPhase,
            IsAnswerPhase = message.IsAnswerPhase,
            IsExecutionFeed = message.IsExecutionFeed,
            IsFeedExpanded = message.IsFeedExpanded,
            ExecutionFeedEntries = message.ExecutionFeedEntries.Select(entry => new WorkspaceFeedEntryRecord
            {
                Kind = entry.Kind,
                Section = entry.Section.ToString(),
                Text = entry.Text,
                Level = entry.Level,
                TimestampUtc = entry.Timestamp.ToString("O"),
                ShowSectionHeader = entry.ShowSectionHeader,
                IsStreaming = false,
                Artifact = string.IsNullOrWhiteSpace(entry.ArtifactPath)
                    ? null
                    : new WorkspaceArtifactRecord
                    {
                        Label = entry.ArtifactLabel,
                        Path = entry.ArtifactPath,
                        Kind = "file"
                    }
            }).ToList(),
            Artifacts = message.Artifacts.Select(artifact => new WorkspaceArtifactRecord
            {
                Label = artifact.Label,
                Path = artifact.Path,
                Kind = artifact.Kind
            }).ToList()
        };
    }

    private static ChatMessageViewModel CreateMessageFromRecord(WorkspaceChatMessageRecord record)
    {
        var message = new ChatMessageViewModel(record.Role, record.Text);
        if (!string.IsNullOrWhiteSpace(record.Status))
        {
            message.UpdateStatus(record.Status, string.IsNullOrWhiteSpace(record.StatusBrushResourceKey) ? "TokyoAccentBrush" : record.StatusBrushResourceKey);
        }

        if (record.IsExecutionFeed)
        {
            message.MarkExecutionFeed();
            message.IsFeedExpanded = record.IsFeedExpanded;
            foreach (var entry in record.ExecutionFeedEntries)
            {
                var feedEntry = new ChatExecutionFeedEntryViewModel
                {
                    Kind = entry.Kind,
                    Section = Enum.TryParse<ChatExecutionFeedSection>(entry.Section, true, out var section)
                        ? section
                        : ChatExecutionFeedSection.System,
                    Text = entry.Text,
                    Level = entry.Level,
                    Timestamp = DateTimeOffset.TryParse(entry.TimestampUtc, out var timestamp)
                        ? timestamp
                        : DateTimeOffset.UtcNow,
                    ShowSectionHeader = entry.ShowSectionHeader,
                    IsStreaming = false
                };
                if (entry.Artifact is not null)
                {
                    feedEntry.ArtifactLabel = entry.Artifact.Label;
                    feedEntry.ArtifactPath = entry.Artifact.Path;
                }

                message.AppendFeedEntry(feedEntry);
            }

            message.SetFeedCompleted();
        }
        else if (record.IsThinkingPhase)
        {
            message.MarkThinkingPhase();
            message.SetThinkingText(record.ThinkingText, record.IsThinkingExpanded);
        }
        else if (record.IsAnswerPhase)
        {
            message.MarkAnswerPhase();
        }

        if (!string.IsNullOrWhiteSpace(record.ThinkingText))
        {
            message.SetThinkingText(record.ThinkingText, record.IsThinkingExpanded);
        }

        if (record.Artifacts.Count > 0)
        {
            message.SetArtifacts(record.Artifacts.Select(artifact => new WrapperArtifact
            {
                Label = artifact.Label,
                Path = artifact.Path,
                Kind = artifact.Kind
            }));
        }

        return message;
    }

    private static string? ExtractAudioPath(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var match = Regex.Match(text, @"(?im)^audio:\s*(.+?)\s*$", RegexOptions.Multiline);
        return match.Success
            ? match.Groups[1].Value.Trim().Trim('"').Trim('\'')
            : null;
    }

    private static string BuildPreview(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var singleLine = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return singleLine.Length <= 160 ? singleLine : $"{singleLine[..160]}...";
    }

    public bool ApplyTemplateById(string? templateId, string? wrapperId, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(templateId))
        {
            error = "Template id is required.";
            return false;
        }

        var template = AvailableTemplates.FirstOrDefault(candidate => string.Equals(candidate.Id, templateId, StringComparison.OrdinalIgnoreCase));
        if (template is null)
        {
            error = $"Template '{templateId}' was not found.";
            return false;
        }

        SelectedTemplate = template;
        var effectiveWrapperId = !string.IsNullOrWhiteSpace(wrapperId)
            ? wrapperId
            : template.DefaultWrapperId;
        if (!string.IsNullOrWhiteSpace(effectiveWrapperId))
        {
            var wrapper = AvailableWrappers.FirstOrDefault(candidate => string.Equals(candidate.Id, effectiveWrapperId, StringComparison.OrdinalIgnoreCase));
            if (wrapper is null)
            {
                error = $"Wrapper '{effectiveWrapperId}' was not found.";
                return false;
            }

            SelectedWrapper = wrapper;
        }

        error = string.Empty;
        return true;
    }

    private TemplateDefinition? ResolveEffectiveTemplate()
    {
        if (SelectedTemplate is not null)
        {
            return SelectedTemplate;
        }

        var agentTemplate = ResolveTemplateForAgent(ResolveEffectiveWrapper());
        if (agentTemplate is not null)
        {
            return agentTemplate;
        }

        return AvailableTemplates.FirstOrDefault(template => string.Equals(template.Id, "direct_chat", StringComparison.OrdinalIgnoreCase))
            ?? AvailableTemplates.FirstOrDefault();
    }

    private WrapperDefinition? ResolveEffectiveWrapper()
    {
        if (SelectedWrapper is not null)
        {
            return SelectedWrapper;
        }

        if (SelectedTemplate is not null && !string.IsNullOrWhiteSpace(SelectedTemplate.DefaultWrapperId))
        {
            var templateWrapper = _services.Wrappers.FindById(SelectedTemplate.DefaultWrapperId);
            if (templateWrapper is not null)
            {
                return templateWrapper;
            }
        }

        return _services.Wrappers.FindById("direct_llm");
    }

    private void ApplyTemplateDefaults(TemplateDefinition? template)
    {
        if (template is null)
        {
            return;
        }

        var desiredWrapperId = !string.IsNullOrWhiteSpace(template.DefaultWrapperId)
            ? template.DefaultWrapperId
            : "direct_llm";
        var desiredWrapper = _services.Wrappers.FindById(desiredWrapperId);
        if (desiredWrapper is not null && !string.Equals(SelectedWrapper?.Id, desiredWrapper.Id, StringComparison.OrdinalIgnoreCase))
        {
            SelectedWrapper = desiredWrapper;
        }
    }

    private void UpdateWrapperSelectionState()
    {
        var effectiveWrapper = ResolveEffectiveWrapper();
        var effectiveTemplate = ResolveEffectiveTemplate();
        CanAttachArtifacts = (effectiveWrapper?.SupportsArtifacts ?? false) ||
            (effectiveTemplate?.Inputs.Any(input => string.Equals(input.Kind, "files", StringComparison.OrdinalIgnoreCase)) ?? false);
        if (!CanAttachArtifacts)
        {
            ShowArtifactInputBox = false;
        }
        SelectedTemplateStatusText = effectiveTemplate is null
            ? "Agent defaults active"
            : $"{effectiveWrapper?.Name ?? "Agent"} • {effectiveTemplate.Name}";
        RefreshExposedAgentSettings(effectiveWrapper);
    }

    private void RefreshExposedAgentSettings(WrapperDefinition? wrapper)
    {
        var existingValues = ExposedAgentSettings.ToDictionary(setting => setting.Id, setting => setting.Value, StringComparer.OrdinalIgnoreCase);
        ExposedAgentSettings.Clear();
        foreach (var setting in wrapper?.Settings.Where(setting => setting.Exposed && !string.IsNullOrWhiteSpace(setting.Id)) ?? Enumerable.Empty<WrapperSettingDefinition>())
        {
            var viewModel = new WrapperSettingViewModel(setting, _ => PersistActiveWorkspaceSession());
            if (existingValues.TryGetValue(viewModel.Id, out var previousValue) && !string.IsNullOrWhiteSpace(previousValue))
            {
                viewModel.Value = previousValue;
            }

            ExposedAgentSettings.Add(viewModel);
        }
    }

    private Dictionary<string, string> GetWrapperSettingValues(WrapperDefinition wrapper)
    {
        var values = wrapper.Settings
            .Where(setting => !string.IsNullOrWhiteSpace(setting.Id))
            .ToDictionary(
                setting => setting.Id,
                setting => string.IsNullOrWhiteSpace(setting.Value) ? setting.DefaultValue : setting.Value,
                StringComparer.OrdinalIgnoreCase);

        foreach (var exposed in ExposedAgentSettings)
        {
            if (!string.IsNullOrWhiteSpace(exposed.Id))
            {
                values[exposed.Id] = exposed.Value;
            }
        }

        return values;
    }

    private WrapperDefinition? GetDefaultAgent(IEnumerable<WrapperDefinition>? availableAgents = null)
    {
        var agents = (availableAgents ?? AvailableAgents).ToList();
        return agents.FirstOrDefault(wrapper => string.Equals(wrapper.Id, "nexusglmharness", StringComparison.OrdinalIgnoreCase))
            ?? agents.FirstOrDefault(wrapper => string.Equals(wrapper.Id, "nexusagent", StringComparison.OrdinalIgnoreCase))
            ?? agents.FirstOrDefault(wrapper => string.Equals(wrapper.Id, "direct_llm", StringComparison.OrdinalIgnoreCase))
            ?? agents.FirstOrDefault();
    }

    private TemplateDefinition? ResolveTemplateForAgent(WrapperDefinition? agent)
    {
        if (agent is null)
        {
            return null;
        }

        if (string.Equals(agent.Id, "direct_llm", StringComparison.OrdinalIgnoreCase))
        {
            return AvailableTemplates.FirstOrDefault(template => string.Equals(template.Id, "direct_chat", StringComparison.OrdinalIgnoreCase));
        }

        return AvailableTemplates.FirstOrDefault(template =>
                   string.Equals(template.DefaultWrapperId, agent.Id, StringComparison.OrdinalIgnoreCase) &&
                   template.Tags.Contains("chat", StringComparer.OrdinalIgnoreCase))
               ?? AvailableTemplates.FirstOrDefault(template => string.Equals(template.DefaultWrapperId, agent.Id, StringComparison.OrdinalIgnoreCase))
               ?? AvailableTemplates.FirstOrDefault(template => template.AllowedWrapperIds.Contains(agent.Id, StringComparer.OrdinalIgnoreCase));
    }

    private void ApplyCurrentAgentDefaults()
    {
        var agent = SelectedAgent;
        if (agent is null)
        {
            return;
        }

        _applyingAgentDefaults = true;
        try
        {
            var effective = _services.AgentConfigs.ResolveEffective(agent);

            var template = !string.IsNullOrWhiteSpace(effective.TemplateId)
                ? AvailableTemplates.FirstOrDefault(candidate => string.Equals(candidate.Id, effective.TemplateId, StringComparison.OrdinalIgnoreCase))
                : ResolveTemplateForAgent(agent);
            if (template is not null && !string.Equals(template.Id, SelectedTemplate?.Id, StringComparison.OrdinalIgnoreCase))
            {
                SelectedTemplate = template;
            }

            if (!string.IsNullOrWhiteSpace(effective.DefaultModelId))
            {
                TrySelectModelOption(effective.DefaultModelId, null, out _);
            }
        }
        finally
        {
            _applyingAgentDefaults = false;
        }

        UpdateSelectionState();
    }

    private static int GetAgentSortOrder(string agentId) =>
        agentId.ToLowerInvariant() switch
        {
            "nexusglmharness" => 0,
            "nexusagent" => 1,
            "direct_llm" => 2,
            _ => 10
        };

    partial void OnSessionIdChanged(string value) => OnPropertyChanged(nameof(ShortSessionId));

    partial void OnLatestRunIdChanged(string value) => OnPropertyChanged(nameof(ShortLatestRunId));

    private static string ShortenIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "none";
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 12
            ? trimmed
            : $"{trimmed[..6]}...{trimmed[^4..]}";
    }

    private static List<string> ParseArtifactInputs(string? text)
    {
        return (text ?? string.Empty)
            .Split(new[] { '\n', ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void ApplyWrapperCapabilities(WrapperDefinition? wrapper)
    {
        UpdateSelectionState();
    }

    private static bool IsDirectLlmWrapper(WrapperDefinition? wrapper) =>
        string.Equals(wrapper?.Id, "direct_llm", StringComparison.OrdinalIgnoreCase);

    private DirectChatExecutionPlan? ResolveCurrentDirectChatPlan() =>
        IsDirectLlmWrapper(ResolveEffectiveWrapper())
            ? _services.DirectChatExecutionPolicy.Resolve(SelectedModel?.InstanceId, SelectedModel?.ModelId, MaxTokens)
            : null;

    private void AppendDirectChatErrorMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var errorMessage = new ChatMessageViewModel("error", message);
        errorMessage.UpdateStatus("Error", "TokyoErrorBrush");
        Messages.Add(errorMessage);
        GenerationStatusText = "Error";
        ChatView.RequestTranscriptScrollToBottom();
        PersistActiveWorkspaceSession();
    }

    private static bool ShouldResetGenerationStatusToIdle(string statusText) =>
        string.IsNullOrWhiteSpace(statusText) ||
        string.Equals(statusText, "Idle", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(statusText, "Queued", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(statusText, "Typing", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(statusText, "Running", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(statusText, "Running Wrapper", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(statusText, "Running Workflow", StringComparison.OrdinalIgnoreCase);

    private LoadedInstanceSnapshot? ResolveAutoChatInstance() =>
        IsDirectLlmWrapper(ResolveEffectiveWrapper())
            ? _services.Library.ResolveDirectChatAutoInstance()
            : _services.Library.ResolveInstanceForEndpoint("thinking");

    private sealed class BatchedRuntimeStreamProgress : IProgress<RuntimeStreamEvent>, IDisposable
    {
        private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(80);
        private readonly object _lock = new();
        private readonly Action<RuntimeStreamEvent> _onFlush;
        private readonly StringBuilder _thinking = new();
        private readonly StringBuilder _answer = new();
        private bool _flushScheduled;
        private bool _disposed;

        public BatchedRuntimeStreamProgress(Action<RuntimeStreamEvent> onFlush)
        {
            _onFlush = onFlush;
        }

        public void Report(RuntimeStreamEvent value)
        {
            if (string.IsNullOrEmpty(value.Text))
            {
                return;
            }

            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                if (value.IsThinking)
                {
                    _thinking.Append(value.Text);
                }
                else
                {
                    _answer.Append(value.Text);
                }

                if (_flushScheduled)
                {
                    return;
                }

                _flushScheduled = true;
            }

            _ = Task.Delay(FlushInterval).ContinueWith(
                _ => Dispatcher.UIThread.Post(FlushOnUiThread, DispatcherPriority.Background),
                TaskScheduler.Default);
        }

        public Task FlushAsync()
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    FlushOnUiThread();
                    completion.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            }, DispatcherPriority.Send);
            return completion.Task;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _disposed = true;
            }
        }

        private void FlushOnUiThread()
        {
            string thinking;
            string answer;
            lock (_lock)
            {
                thinking = _thinking.ToString();
                answer = _answer.ToString();
                _thinking.Clear();
                _answer.Clear();
                _flushScheduled = false;
            }

            if (thinking.Length > 0)
            {
                _onFlush(new RuntimeStreamEvent(RuntimeStreamPhase.Thinking, thinking));
            }

            if (answer.Length > 0)
            {
                _onFlush(new RuntimeStreamEvent(RuntimeStreamPhase.Answer, answer));
            }
        }
    }

    private async Task LoadPreviewContentAsync(string path)
    {
        try
        {
            var content = await Task.Run(() =>
            {
                if (!File.Exists(path)) return string.Empty;
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                var buffer = new char[2000];
                var read = reader.ReadBlock(buffer, 0, 2000);
                return new string(buffer, 0, read);
            });
            PreviewFileContent = content;
            IsPreviewVisible = true;
        }
        catch
        {
            IsPreviewVisible = false;
        }
    }

    [RelayCommand]
    private void ClosePreview()
    {
        IsPreviewVisible = false;
    }
}

public partial class ChatMessageViewModel : ObservableObject
{
    private const int FeedCollapseThreshold = 14;

    [ObservableProperty]
    private string _role;

    [ObservableProperty]
    private string _text;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _statusBrushResourceKey = "TokyoAccentBrush";

    [ObservableProperty]
    private ObservableCollection<ChatRenderBlockViewModel> _blocks = new();

    [ObservableProperty]
    private string _thinkingText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ChatRenderBlockViewModel> _thinkingBlocks = new();

    [ObservableProperty]
    private string _reasoningSummaryText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ChatRenderBlockViewModel> _reasoningSummaryBlocks = new();

    [ObservableProperty]
    private bool _isThinkingExpanded;

    [ObservableProperty]
    private bool _isThinkingPhase;

    [ObservableProperty]
    private bool _isAnswerPhase;

    [ObservableProperty]
    private string _liveThinkingText = string.Empty;

    [ObservableProperty]
    private string _liveAnswerText = string.Empty;

    [ObservableProperty]
    private bool _isStreamingThinking;

    [ObservableProperty]
    private bool _isStreamingAnswer;

    [ObservableProperty]
    private bool _isPlainChatPresentation;

    [ObservableProperty]
    private bool _showUserToAiDivider;

    [ObservableProperty]
    private bool _hasAudio;

    [ObservableProperty]
    private string? _audioPath;

    [ObservableProperty]
    private string _phaseLabel = "Think";

    [ObservableProperty]
    private ObservableCollection<WrapperArtifactViewModel> _artifacts = new();

    [ObservableProperty]
    private bool _isExecutionFeed;

    [ObservableProperty]
    private ObservableCollection<ChatExecutionFeedEntryViewModel> _executionFeedEntries = new();

    [ObservableProperty]
    private ObservableCollection<ChatExecutionFeedChainViewModel> _executionFeedChains = new();

    [ObservableProperty]
    private bool _isFeedExpanded = true;

    [ObservableProperty]
    private bool _isExecutionFeedStreaming;

    [ObservableProperty]
    private ObservableCollection<ChatRenderBlockViewModel> _executionFeedBlocks = new();

    [ObservableProperty]
    private string _streamingExecutionFeedBulletText = string.Empty;

    private readonly Dictionary<string, ChatExecutionFeedChainViewModel> _executionFeedChainIndex = new(StringComparer.OrdinalIgnoreCase);

    public ChatMessageViewModel(string role, string text, bool hasAudio = false, string? audioPath = null)
    {
        _role = role;
        _text = text;
        HasAudio = hasAudio;
        AudioPath = audioPath;
        SetText(text);
    }

    public bool ShowStatus => !string.IsNullOrWhiteSpace(Status);
    public bool IsAssistantLike => !string.Equals(Role, "user", StringComparison.OrdinalIgnoreCase);
    public bool HasThinkingText => !string.IsNullOrWhiteSpace(ThinkingText);
    public bool HasReasoningSummary => !string.IsNullOrWhiteSpace(ReasoningSummaryText);
    public bool ShowSeparatedResponseBlock => IsAssistantLike && HasThinkingText;
    public bool IsStreaming => IsStreamingThinking || IsStreamingAnswer || IsExecutionFeedStreaming;
    public bool IsPhaseMessage => IsThinkingPhase || IsAnswerPhase || IsExecutionFeed;
    public bool ShowStandardTextBlocks => !IsPhaseMessage && !IsPlainChatPresentation;
    public bool ShowPlainTypingStatus => IsPlainChatPresentation && IsStreamingAnswer;
    public bool HasLiveThinkingText => !string.IsNullOrWhiteSpace(LiveThinkingText);
    public bool HasLiveAnswerText => !string.IsNullOrWhiteSpace(LiveAnswerText);
    public bool HasArtifacts => Artifacts.Count > 0;
    public bool HasExecutionFeedEntries => ExecutionFeedEntries.Count > 0 || ExecutionFeedChains.Count > 0;
    public bool HasExecutionFeedChains => ExecutionFeedChains.Count > 0;
    public bool HasFlatExecutionFeedEntries => ExecutionFeedEntries.Count > 0;
    public bool CanCollapseFeed => ExecutionFeedEntries.Count + _executionFeedChainIndex.Count > FeedCollapseThreshold;
    public bool HasExecutionFeedDigest => IsExecutionFeed;
    public bool HasExecutionFeedIssues => ExecutionFeedIssueCount > 0;
    public int ExecutionFeedIssueCount => CountIssueEntries(ExecutionFeedEntries) + EnumerateChains(ExecutionFeedChains).Sum(chain => CountIssueEntries(chain.Entries));
    public int ExecutionFeedArtifactCount => Artifacts.Count + CountArtifactEntries(ExecutionFeedEntries) + EnumerateChains(ExecutionFeedChains).Sum(chain => CountArtifactEntries(chain.Entries));
    public string ExecutionFeedIssueSummaryText => ExecutionFeedIssueCount == 1 ? "1 finding" : $"{ExecutionFeedIssueCount} findings";
    public string ExecutionFeedArtifactSummaryText => ExecutionFeedArtifactCount == 1 ? "1 artifact" : $"{ExecutionFeedArtifactCount} artifacts";
    public string ExecutionFeedLastUpdateText
    {
        get
        {
            var latest = ExecutionFeedEntries.Select(entry => (DateTimeOffset?)entry.Timestamp)
                .Concat(EnumerateChains(ExecutionFeedChains).SelectMany(chain => chain.Entries.Select(entry => (DateTimeOffset?)entry.Timestamp)))
                .Where(value => value.HasValue)
                .Max();
            return latest.HasValue ? $"updated {latest.Value.ToLocalTime():HH:mm:ss}" : "waiting for events";
        }
    }
    public string ExecutionFeedCurrentStepText
    {
        get
        {
            var runningChain = EnumerateChains(ExecutionFeedChains).LastOrDefault(chain => chain.IsRunning);
            if (runningChain is not null)
            {
                return $"Now: {runningChain.HeaderText}";
            }

            if (!string.IsNullOrWhiteSpace(StreamingExecutionFeedBulletText))
            {
                return $"Now: {TrimForDigest(StreamingExecutionFeedBulletText)}";
            }

            var latestEntry = ExecutionFeedEntries
                .Concat(EnumerateChains(ExecutionFeedChains).SelectMany(chain => chain.Entries))
                .OrderBy(entry => entry.Timestamp)
                .LastOrDefault();
            if (latestEntry is not null)
            {
                return $"{latestEntry.BulletLabel}: {TrimForDigest(latestEntry.BulletBodyText)}";
            }

            return IsExecutionFeedStreaming ? "Starting run..." : "No run events yet.";
        }
    }
    public string ExecutionFeedTranscript => string.Join(
        Environment.NewLine,
        ExecutionFeedEntries.Select(entry => entry.TranscriptText)
            .Concat(ExecutionFeedChains.Select(chain => chain.TranscriptText))
            .Where(text => !string.IsNullOrWhiteSpace(text)));
    public bool HasStreamingExecutionFeedBullet => !string.IsNullOrWhiteSpace(StreamingExecutionFeedBulletText);

    public void AppendLiveChunk(RuntimeStreamEvent chunk)
    {
        if (chunk.IsThinking)
        {
            IsStreamingThinking = true;
            LiveThinkingText = string.Concat(LiveThinkingText, chunk.Text);
            ThinkingBlocks = new ObservableCollection<ChatRenderBlockViewModel>(ChatMessageFormatter.ParseBlocks(LiveThinkingText));
        }
        else
        {
            IsStreamingAnswer = true;
            LiveAnswerText = string.Concat(LiveAnswerText, chunk.Text);
            Blocks = new ObservableCollection<ChatRenderBlockViewModel>(ChatMessageFormatter.ParseBlocks(LiveAnswerText));
        }
    }

    public void ClearLiveStream()
    {
        LiveThinkingText = string.Empty;
        LiveAnswerText = string.Empty;
        IsStreamingThinking = false;
        IsStreamingAnswer = false;
        OnPropertyChanged(nameof(IsStreaming));
        OnPropertyChanged(nameof(HasLiveThinkingText));
        OnPropertyChanged(nameof(HasLiveAnswerText));
    }

    public void MarkExecutionFeed()
    {
        IsExecutionFeed = true;
        PhaseLabel = "Execution Feed";
    }

    public void AppendFeedEntry(ChatExecutionFeedEntryViewModel entry)
    {
        FinalizeStreamingFeedEntries();
        var previous = ExecutionFeedEntries.LastOrDefault(feedEntry => !string.IsNullOrWhiteSpace(feedEntry.TranscriptText));
        entry.ShowSectionHeader = previous is null || previous.Section != entry.Section;
        ExecutionFeedEntries.Add(entry);
        IsExecutionFeedStreaming = entry.IsStreaming;
        SyncExecutionFeedState();
    }

    public void AppendChainedFeedEvent(WrapperRunStreamEvent streamEvent, ChatExecutionFeedSection section, string kind, string text, string level)
    {
        if (string.IsNullOrWhiteSpace(streamEvent.ChainId))
        {
            AppendFeedEntry(new ChatExecutionFeedEntryViewModel
            {
                Section = section,
                Kind = kind,
                Text = text,
                Level = level,
                Timestamp = DateTimeOffset.UtcNow,
                IsStreaming = streamEvent.IsTokenStream,
                ValidationStatus = streamEvent.ValidationStatus,
                ApprovalId = streamEvent.ApprovalId,
                ToolName = streamEvent.ToolName,
                MemoryId = streamEvent.MemoryId
            });
            return;
        }

        var chain = GetOrCreateExecutionFeedChain(streamEvent);
        if (streamEvent.IsTokenStream)
        {
            chain.AppendStreamingText(text);
            IsExecutionFeedStreaming = true;
        }
        else if (!streamEvent.IsChainStart || !string.IsNullOrWhiteSpace(text))
        {
            chain.AddEntry(new ChatExecutionFeedEntryViewModel
            {
                Section = section,
                Kind = kind,
                Text = text,
                Level = level,
                Timestamp = DateTimeOffset.UtcNow,
                ValidationStatus = streamEvent.ValidationStatus,
                ApprovalId = streamEvent.ApprovalId,
                ToolName = streamEvent.ToolName,
                MemoryId = streamEvent.MemoryId
            });
        }

        if (streamEvent.IsChainEnd)
        {
            var status = string.Equals(level, "error", StringComparison.OrdinalIgnoreCase) ? "blocked" : "done";
            chain.Complete(status, level);
        }

        SyncExecutionFeedState();
    }

    public void AppendStreamingFeedText(ChatExecutionFeedSection section, string kind, string text, string linePrefix, string level)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var current = ExecutionFeedEntries.LastOrDefault();
        if (current is not null &&
            current.IsStreaming &&
            current.Section == section &&
            string.Equals(current.Kind, kind, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(current.Level, level, StringComparison.OrdinalIgnoreCase))
        {
            current.Text = string.Concat(current.Text, text);
            current.Timestamp = DateTimeOffset.UtcNow;
        }
        else
        {
            FinalizeStreamingFeedEntries();
            AppendFeedEntry(new ChatExecutionFeedEntryViewModel
            {
                Section = section,
                Kind = kind,
                Text = string.Concat(linePrefix, ": ", text),
                Level = level,
                Timestamp = DateTimeOffset.UtcNow,
                IsStreaming = true
            });
            return;
        }

        SyncExecutionFeedState();
    }

    public void SetFeedCompleted()
    {
        FinalizeStreamingFeedEntries();
        IsExecutionFeedStreaming = false;
        ThinkingText = ExecutionFeedTranscript;
        RefreshThinkingPresentation();
        if (CanCollapseFeed)
        {
            IsFeedExpanded = false;
        }

        SyncExecutionFeedState();
    }

    [RelayCommand]
    private void ExpandExecutionFeed()
    {
        IsFeedExpanded = true;
        foreach (var chain in ExecutionFeedChains)
        {
            chain.ExpandAll();
        }
    }

    [RelayCommand]
    private void CollapseExecutionFeed()
    {
        foreach (var chain in ExecutionFeedChains)
        {
            chain.CollapseAll();
        }
    }

    public void SetText(string text)
    {
        Text = text;
        Blocks = new ObservableCollection<ChatRenderBlockViewModel>(ChatMessageFormatter.ParseBlocks(text));
        OnPropertyChanged(nameof(IsAssistantLike));
        OnPropertyChanged(nameof(ShowSeparatedResponseBlock));
    }

    public void UpdateStatus(string status, string brushResourceKey)
    {
        Status = status;
        StatusBrushResourceKey = brushResourceKey;
        OnPropertyChanged(nameof(ShowStatus));
    }

    public void MarkThinkingPhase()
    {
        IsThinkingPhase = true;
    }

    public void MarkAnswerPhase()
    {
        IsAnswerPhase = true;
    }

    public void MarkPlainChatPresentation()
    {
        IsPlainChatPresentation = true;
        IsExecutionFeed = false;
        IsThinkingPhase = false;
        IsAnswerPhase = false;
    }

    public void SetThinkingText(string text, bool expanded = false)
    {
        ThinkingText = text ?? string.Empty;
        RefreshThinkingPresentation();
        IsThinkingExpanded = expanded;
        OnPropertyChanged(nameof(HasThinkingText));
        OnPropertyChanged(nameof(HasReasoningSummary));
        OnPropertyChanged(nameof(ShowSeparatedResponseBlock));
    }

    public void SetArtifacts(IEnumerable<WrapperArtifact> artifacts)
    {
        Artifacts = new ObservableCollection<WrapperArtifactViewModel>(
            artifacts.Select(artifact => new WrapperArtifactViewModel(artifact.Label, artifact.Path, artifact.Kind)));
        OnPropertyChanged(nameof(HasArtifacts));
        OnPropertyChanged(nameof(ExecutionFeedArtifactCount));
        OnPropertyChanged(nameof(ExecutionFeedArtifactSummaryText));
    }

    partial void OnLiveThinkingTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsStreaming));
        OnPropertyChanged(nameof(HasLiveThinkingText));
    }

    partial void OnLiveAnswerTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsStreaming));
        OnPropertyChanged(nameof(HasLiveAnswerText));
        OnPropertyChanged(nameof(ShowPlainTypingStatus));
    }

    partial void OnIsStreamingThinkingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsStreaming));
    }

    partial void OnIsStreamingAnswerChanged(bool value)
    {
        OnPropertyChanged(nameof(IsStreaming));
        OnPropertyChanged(nameof(ShowPlainTypingStatus));
    }

    partial void OnIsThinkingPhaseChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPhaseMessage));
        OnPropertyChanged(nameof(ShowStandardTextBlocks));
    }

    partial void OnIsAnswerPhaseChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPhaseMessage));
        OnPropertyChanged(nameof(ShowStandardTextBlocks));
    }

    partial void OnIsExecutionFeedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPhaseMessage));
        OnPropertyChanged(nameof(ShowStandardTextBlocks));
    }

    partial void OnIsPlainChatPresentationChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowStandardTextBlocks));
        OnPropertyChanged(nameof(ShowPlainTypingStatus));
    }

    private void FinalizeStreamingFeedEntries()
    {
        var hadStreaming = false;
        foreach (var entry in ExecutionFeedEntries.Where(entry => entry.IsStreaming))
        {
            entry.IsStreaming = false;
            hadStreaming = true;
        }

        if (hadStreaming)
        {
            IsExecutionFeedStreaming = false;
        }
    }

    private void SyncExecutionFeedState()
    {
        LiveThinkingText = ExecutionFeedTranscript;
        if (!IsExecutionFeedStreaming)
        {
            ThinkingText = ExecutionFeedTranscript;
            RefreshThinkingPresentation();
        }

        var completedTranscript = string.Join(
            Environment.NewLine,
            ExecutionFeedEntries
                .Where(entry => !entry.IsStreaming)
                .Select(entry => entry.BulletText)
                .Concat(ExecutionFeedChains.Select(chain => chain.TranscriptText)));
        ExecutionFeedBlocks = string.IsNullOrWhiteSpace(completedTranscript)
            ? new ObservableCollection<ChatRenderBlockViewModel>()
            : new ObservableCollection<ChatRenderBlockViewModel>(ChatMessageFormatter.ParseBlocks(completedTranscript));

        StreamingExecutionFeedBulletText = ExecutionFeedEntries.LastOrDefault(entry => entry.IsStreaming)?.BulletText ?? string.Empty;

        OnPropertyChanged(nameof(HasExecutionFeedEntries));
        OnPropertyChanged(nameof(HasExecutionFeedChains));
        OnPropertyChanged(nameof(HasFlatExecutionFeedEntries));
        OnPropertyChanged(nameof(CanCollapseFeed));
        OnPropertyChanged(nameof(ExecutionFeedTranscript));
        OnPropertyChanged(nameof(HasStreamingExecutionFeedBullet));
        OnPropertyChanged(nameof(HasExecutionFeedDigest));
        OnPropertyChanged(nameof(HasExecutionFeedIssues));
        OnPropertyChanged(nameof(ExecutionFeedIssueCount));
        OnPropertyChanged(nameof(ExecutionFeedIssueSummaryText));
        OnPropertyChanged(nameof(ExecutionFeedArtifactCount));
        OnPropertyChanged(nameof(ExecutionFeedArtifactSummaryText));
        OnPropertyChanged(nameof(ExecutionFeedLastUpdateText));
        OnPropertyChanged(nameof(ExecutionFeedCurrentStepText));
        OnPropertyChanged(nameof(IsStreaming));
        OnPropertyChanged(nameof(HasLiveThinkingText));
        OnPropertyChanged(nameof(HasThinkingText));
        OnPropertyChanged(nameof(HasReasoningSummary));
        OnPropertyChanged(nameof(ShowSeparatedResponseBlock));
    }

    private ChatExecutionFeedChainViewModel GetOrCreateExecutionFeedChain(WrapperRunStreamEvent streamEvent)
    {
        if (_executionFeedChainIndex.TryGetValue(streamEvent.ChainId, out var existing))
        {
            ApplyChainMetadata(existing, streamEvent);
            return existing;
        }

        var chain = new ChatExecutionFeedChainViewModel
        {
            ChainId = streamEvent.ChainId,
            ParentChainId = streamEvent.ParentChainId,
            StepId = string.IsNullOrWhiteSpace(streamEvent.StepId) ? streamEvent.ChainId : streamEvent.StepId,
            Phase = streamEvent.Phase,
            Depth = streamEvent.Depth,
            Title = string.IsNullOrWhiteSpace(streamEvent.Title) ? streamEvent.ChainId : streamEvent.Title,
            Level = string.IsNullOrWhiteSpace(streamEvent.Level) ? "info" : streamEvent.Level,
            Status = streamEvent.IsChainEnd ? "done" : "running",
            IsRunning = !streamEvent.IsChainEnd,
            IsExpanded = streamEvent.Depth <= 1 || !streamEvent.IsChainEnd
        };
        _executionFeedChainIndex[chain.ChainId] = chain;

        if (!string.IsNullOrWhiteSpace(chain.ParentChainId) &&
            _executionFeedChainIndex.TryGetValue(chain.ParentChainId, out var parent) &&
            !parent.Children.Any(child => string.Equals(child.ChainId, chain.ChainId, StringComparison.OrdinalIgnoreCase)))
        {
            parent.AddChild(chain);
        }
        else if (!ExecutionFeedChains.Any(item => string.Equals(item.ChainId, chain.ChainId, StringComparison.OrdinalIgnoreCase)))
        {
            ExecutionFeedChains.Add(chain);
        }

        OnPropertyChanged(nameof(HasExecutionFeedChains));
        return chain;
    }

    private static IEnumerable<ChatExecutionFeedChainViewModel> EnumerateChains(IEnumerable<ChatExecutionFeedChainViewModel> chains)
    {
        foreach (var chain in chains)
        {
            yield return chain;
            foreach (var child in EnumerateChains(chain.Children))
            {
                yield return child;
            }
        }
    }

    private static int CountIssueEntries(IEnumerable<ChatExecutionFeedEntryViewModel> entries) =>
        entries.Count(entry =>
            string.Equals(entry.Level, "error", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entry.Level, "warning", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entry.Kind, "error", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entry.ValidationStatus, "fail", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entry.ValidationStatus, "failed", StringComparison.OrdinalIgnoreCase));

    private static int CountArtifactEntries(IEnumerable<ChatExecutionFeedEntryViewModel> entries) =>
        entries.Count(entry => entry.HasArtifact || entry.Section == ChatExecutionFeedSection.Artifacts);

    private static string TrimForDigest(string text)
    {
        var normalized = string.Join(" ", (text ?? string.Empty).Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 120 ? normalized : $"{normalized[..117]}...";
    }

    private static void ApplyChainMetadata(ChatExecutionFeedChainViewModel chain, WrapperRunStreamEvent streamEvent)
    {
        if (!string.IsNullOrWhiteSpace(streamEvent.Title))
        {
            chain.Title = streamEvent.Title;
        }

        if (!string.IsNullOrWhiteSpace(streamEvent.Phase))
        {
            chain.Phase = streamEvent.Phase;
        }

        if (!string.IsNullOrWhiteSpace(streamEvent.StepId))
        {
            chain.StepId = streamEvent.StepId;
        }

        if (!string.IsNullOrWhiteSpace(streamEvent.Level))
        {
            chain.Level = streamEvent.Level;
        }
    }

    private void RefreshThinkingPresentation()
    {
        ThinkingBlocks = new ObservableCollection<ChatRenderBlockViewModel>(ChatMessageFormatter.ParseBlocks(ThinkingText));
        ReasoningSummaryText = ThinkingOutputParser.BuildReasoningSummaryText(ThinkingText);
        ReasoningSummaryBlocks = string.IsNullOrWhiteSpace(ReasoningSummaryText)
            ? new ObservableCollection<ChatRenderBlockViewModel>()
            : new ObservableCollection<ChatRenderBlockViewModel>(ChatMessageFormatter.ParseBlocks(ReasoningSummaryText));
        OnPropertyChanged(nameof(HasReasoningSummary));
    }
}

public class WrapperArtifactViewModel
{
    public WrapperArtifactViewModel(string label, string path, string kind)
    {
        Label = string.IsNullOrWhiteSpace(label) ? System.IO.Path.GetFileName(path) : label;
        Path = path;
        Kind = kind;
    }

    public string Label { get; }
    public string Path { get; }
    public string Kind { get; }
}

internal sealed class WorkflowFeedTracker
{
    public string LastRunStatus { get; set; } = string.Empty;
    public string LastCurrentStepId { get; set; } = string.Empty;
    public string LastError { get; set; } = string.Empty;
    public Dictionary<string, WorkflowStepFeedTracker> Steps { get; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class WorkflowStepFeedTracker
{
    public string Status { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string FinalText { get; set; } = string.Empty;
    public int LogCount { get; set; }
    public int ArtifactCount { get; set; }
}

public class ChatModelOptionViewModel
{
    public ChatModelOptionViewModel(string instanceId, string modelId, string displayName, int defaultMaxTokens, string format = "", string runtimeBackend = "", bool isRunnable = true, string runnableReason = "")
    {
        InstanceId = instanceId;
        ModelId = modelId;
        DisplayName = displayName;
        DefaultMaxTokens = defaultMaxTokens;
        Format = format;
        RuntimeBackend = runtimeBackend;
        IsRunnable = isRunnable;
        RunnableReason = runnableReason;
    }

    public string InstanceId { get; }
    public string ModelId { get; }
    public string DisplayName { get; }
    public int DefaultMaxTokens { get; }
    public string Format { get; }
    public string RuntimeBackend { get; }
    public bool IsRunnable { get; }
    public string RunnableReason { get; }
}

public partial class WrapperSettingViewModel : ObservableObject
{
    private readonly Action<WrapperSettingViewModel>? _changed;

    public WrapperSettingViewModel(WrapperSettingDefinition definition, Action<WrapperSettingViewModel>? changed = null)
    {
        _changed = changed;
        Id = definition.Id;
        Label = string.IsNullOrWhiteSpace(definition.Label) ? definition.Id : definition.Label;
        Kind = string.IsNullOrWhiteSpace(definition.Kind) ? "text" : definition.Kind;
        Description = definition.Description;
        Min = definition.Min;
        Max = definition.Max;
        _value = string.IsNullOrWhiteSpace(definition.Value) ? definition.DefaultValue : definition.Value;
        if (string.IsNullOrWhiteSpace(_value) && IsToggle)
        {
            _value = "false";
        }
        ToggleCommand = new RelayCommand(ToggleAndNotify);
    }

    public string Id { get; }
    public string Label { get; }
    public string Kind { get; }
    public string Description { get; }
    public double? Min { get; }
    public double? Max { get; }
    public IRelayCommand ToggleCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    [NotifyPropertyChangedFor(nameof(IsOn))]
    private string _value = string.Empty;

    public bool IsToggle =>
        string.Equals(Kind, "toggle", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Kind, "bool", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Kind, "boolean", StringComparison.OrdinalIgnoreCase);

    public bool IsNumber =>
        string.Equals(Kind, "number", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Kind, "int", StringComparison.OrdinalIgnoreCase);

    public bool IsText => !IsToggle && !IsNumber;

    public bool IsOn => string.Equals(Value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Value, "on", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Value, "1", StringComparison.OrdinalIgnoreCase);

    public string DisplayText => IsToggle ? $"{Label}: {(IsOn ? "on" : "off")}" : $"{Label}: {Value}";

    public void Toggle()
    {
        if (IsToggle)
        {
            Value = IsOn ? "false" : "true";
        }
        else if (IsNumber)
        {
            CycleNumber();
        }
    }

    private void CycleNumber()
    {
        if (!double.TryParse(Value, out var current))
        {
            current = Min ?? 0;
        }

        var min = Min ?? 0;
        var max = Max ?? current + 10;

        // Determine step size based on range magnitude
        var range = max - min;
        double step;
        if (range <= 10) step = 1;
        else if (range <= 100) step = 10;
        else if (range <= 1000) step = 64;
        else step = 256;

        var next = current + step;
        if (next > max) next = min;

        // Use integer formatting when min/max suggest integer values
        Value = (next % 1 == 0) ? ((int)next).ToString() : next.ToString("F1");
    }

    private void ToggleAndNotify()
    {
        Toggle();
    }

    partial void OnValueChanged(string value)
    {
        if (IsNumber && double.TryParse(value, out var parsed))
        {
            if (Min.HasValue && parsed < Min.Value)
            {
                Value = Min.Value.ToString("0.##");
                return;
            }

            if (Max.HasValue && parsed > Max.Value)
            {
                Value = Max.Value.ToString("0.##");
                return;
            }
        }

        _changed?.Invoke(this);
    }
}

public class ChatRunState
{
    public string RunId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string ExecutionMode { get; set; } = "direct_llm";
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public string WrapperId { get; set; } = string.Empty;
    public string WrapperName { get; set; } = string.Empty;
    public string SelectedInstanceId { get; set; } = string.Empty;
    public string SelectedModelId { get; set; } = string.Empty;
    public string SelectedModelLabel { get; set; } = string.Empty;
    public string ResolvedInstanceId { get; set; } = string.Empty;
    public string ResolvedModelId { get; set; } = string.Empty;
    public string ResolvedExecutionMode { get; set; } = string.Empty;
    public string OutputQualityState { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public int MaxTokens { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public int TokensGenerated { get; set; }
    public long ElapsedMs { get; set; }
    public string FinishReason { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string OutputPreview { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string ModelLabel { get; set; } = string.Empty;
    public string Backend { get; set; } = string.Empty;
    public string StreamPhase { get; set; } = string.Empty;
    public bool IsStreaming { get; set; }
    public int TranscriptMessageCount { get; set; }
    public string LiveThinkingText { get; set; } = string.Empty;
    public string LiveAnswerText { get; set; } = string.Empty;
    public DateTimeOffset? LastStreamUpdateUtc { get; set; }
    public string[] ArtifactInputs { get; set; } = Array.Empty<string>();
    public string[] ArtifactOutputs { get; set; } = Array.Empty<string>();

    public ChatRunState Clone()
    {
        var clone = (ChatRunState)MemberwiseClone();
        clone.ArtifactInputs = ArtifactInputs.ToArray();
        clone.ArtifactOutputs = ArtifactOutputs.ToArray();
        return clone;
    }
}

public record ChatSnapshot(
    string ExecutionMode,
    string TemplateId,
    string TemplateName,
    string WorkflowId,
    string WorkflowName,
    string WrapperId,
    string WrapperName,
    IReadOnlyList<string> SupportedInputKinds,
    string ArtifactInputsText,
    string SessionId,
    string SelectedModelId,
    string SelectedInstanceId,
    string SelectedModelLabel,
    string SelectedBackend,
    bool SelectedIsRunnable,
    string ResolvedModelId,
    string ResolvedInstanceId,
    string ResolvedExecutionMode,
    string OutputQualityState,
    string LatestRunId,
    string InputText);
