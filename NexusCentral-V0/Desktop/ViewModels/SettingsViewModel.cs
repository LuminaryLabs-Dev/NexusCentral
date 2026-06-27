using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using NexusCentral_Avalonia.Services;

namespace NexusCentral_Avalonia.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    private SettingsDomainViewModel? _selectedDomain;

    [ObservableProperty]
    private ViewModelBase? _currentPage;

    public SettingsViewModel(AppServices services)
    {
        Theme = new ThemeSettingsViewModel(services);
        Behavior = new BehaviorSettingsViewModel(services);
        System = new SystemSettingsViewModel(services);

        Domains = new ObservableCollection<SettingsDomainViewModel>
        {
            new("Theme", "Color editing, preview, and contrast feedback.", Theme),
            new("Behavior", "Density, compression, and signal controls.", Behavior),
            new("System", "Diagnostics and read-only semantic colors.", System),
        };

        SelectedDomain = Domains[0];
    }

    public ObservableCollection<SettingsDomainViewModel> Domains { get; }

    public ThemeSettingsViewModel Theme { get; }
    public BehaviorSettingsViewModel Behavior { get; }
    public SystemSettingsViewModel System { get; }

    partial void OnSelectedDomainChanged(SettingsDomainViewModel? value)
    {
        CurrentPage = value?.ViewModel;
    }
}
