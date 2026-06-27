using NexusCentral.V3.Core.Config;
using NexusCentral.V3.Core.Execution;
using NexusCentral.V3.Core.Logging;
using NexusCentral.V3.Core.State;
using NexusCentral.V3.Desktop.ViewModels;
using NexusCentral.V3.Features.Lab;
using NexusCentral.V3.Features.Workspaces;

namespace NexusCentral.V3.Desktop;

public sealed class AppCompositionRoot
{
    private AppCompositionRoot(MainShellViewModel shell)
    {
        Shell = shell;
    }

    public MainShellViewModel Shell { get; }

    public static AppCompositionRoot CreateDefault()
    {
        var paths = AppPathSettings.CreateDefault();
        paths.EnsureDirectories();

        var json = new JsonFileStore();
        var logs = new InMemoryAppLogService();
        var executionEvents = new InMemoryExecutionEventStream();
        var workspaceSessions = new FileWorkspaceSessionStore(paths, json);
        var workspaces = new WorkspaceService(paths, json, logs);
        var conversations = new FileLabConversationStore(paths, json);
        var lab = new LabViewModel(workspaces, workspaceSessions, conversations, executionEvents);

        return new AppCompositionRoot(new MainShellViewModel(lab, logs, executionEvents));
    }
}
