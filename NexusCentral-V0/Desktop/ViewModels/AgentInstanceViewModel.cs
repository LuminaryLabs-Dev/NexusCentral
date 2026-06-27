using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NexusCentral_Avalonia.ViewModels;

public partial class AgentInstanceViewModel : ObservableObject
{
    public string Id { get; }

    [ObservableProperty]
    private string _status = "○ Idle";

    [ObservableProperty]
    private string _agentCommand = "nexusagent";

    [ObservableProperty]
    private string _selectedProfile = "None";

    [ObservableProperty]
    private string _outputPath = "./outputs";

    [ObservableProperty]
    private bool _isSelected;

    public AgentInstanceViewModel(string id)
    {
        Id = id;
    }
}
