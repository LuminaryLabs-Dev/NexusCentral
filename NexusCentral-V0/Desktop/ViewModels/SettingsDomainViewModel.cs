namespace NexusCentral_Avalonia.ViewModels;

public sealed class SettingsDomainViewModel
{
    public SettingsDomainViewModel(string name, string description, ViewModelBase viewModel)
    {
        Name = name;
        Description = description;
        ViewModel = viewModel;
    }

    public string Name { get; }
    public string Description { get; }
    public ViewModelBase ViewModel { get; }
}
