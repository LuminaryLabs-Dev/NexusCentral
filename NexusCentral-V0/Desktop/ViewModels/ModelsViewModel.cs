using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using NexusCentral_Avalonia.Services;
using System.Collections.Generic;

namespace NexusCentral_Avalonia.ViewModels;

public partial class ModelsViewModel : ViewModelBase
{
    private readonly AppServices _services;
    private readonly Dictionary<string, ModelItemViewModel> _catalogIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LoadedInstanceCardViewModel> _loadedInstanceIndex = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private string _repoText = "LiquidAI/LFM2.5-1.2B-Thinking-GGUF";

    [ObservableProperty]
    private string _filenameText = "LFM2.5-1.2B-Thinking-Q8_0.gguf";

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _revisionText = "main";

    [ObservableProperty]
    private string _saveToPath;

    [ObservableProperty]
    private string _importPath = string.Empty;

    [ObservableProperty]
    private string _downloadStatusText = "Idle.";

    [ObservableProperty]
    private string _activeDownloadId = string.Empty;

    [ObservableProperty]
    private string _instanceSettingsStatusText = "No loaded instances.";

    [ObservableProperty]
    private string _catalogStatusText = "Scanning...";

    [ObservableProperty]
    private bool _isCatalogIntakeOpen;

    [ObservableProperty]
    private ModelItemViewModel? _selectedCatalogModel;

    public ObservableCollection<ModelItemViewModel> DownloadedModels { get; } = new();
    public ObservableCollection<LoadedInstanceCardViewModel> LoadedInstances { get; } = new();

    public bool HasSelectedCatalogModel => SelectedCatalogModel is not null;
    public bool HasLoadedInstances => LoadedInstances.Count > 0;
    public bool NoLoadedInstances => LoadedInstances.Count == 0;
    public string SelectedCatalogName => SelectedCatalogModel?.Name ?? "Select a catalog model";

    public ModelsViewModel(AppServices services)
    {
        _services = services;
        _saveToPath = _services.Paths.ModelsDirectory;
        RefreshCatalog();
        _services.Catalog.CatalogChanged += RefreshCatalog;
        _services.Downloads.JobUpdated += OnJobUpdated;
        _services.Library.LibraryInventoryChanged += RefreshLoadedInstances;
        _services.Library.InstanceChanged += _ => RefreshLoadedInstances();
        RefreshLoadedInstances();
    }

    partial void OnSelectedCatalogModelChanged(ModelItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedCatalogModel));
        OnPropertyChanged(nameof(SelectedCatalogName));
    }

    [RelayCommand]
    private void ToggleCatalogIntake()
    {
        IsCatalogIntakeOpen = !IsCatalogIntakeOpen;
    }

    [RelayCommand]
    private void CloseCatalogIntake()
    {
        IsCatalogIntakeOpen = false;
    }

    [RelayCommand]
    private async Task BrowseSaveTo()
    {
        SaveToPath = _services.Paths.ModelsDirectory;
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task BrowseImportPath()
    {
        ImportPath = _services.Paths.ModelsDirectory;
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void ImportLocalModel()
    {
        if (string.IsNullOrWhiteSpace(ImportPath)) return;

        _services.Catalog.ImportLocalModel(ImportPath);
        _services.Logs.Append("info", "models", $"Imported local model from {ImportPath}.");
        ImportPath = string.Empty;
        DownloadStatusText = "Imported local model into catalog.";
        IsCatalogIntakeOpen = false;
    }

    [RelayCommand]
    private Task DownloadModel()
    {
        if (string.IsNullOrWhiteSpace(RepoText) || string.IsNullOrWhiteSpace(FilenameText)) return Task.CompletedTask;

        var job = _services.Downloads.StartDownload(RepoText, FilenameText, RevisionText);
        ActiveDownloadId = job.DownloadId;
        IsDownloading = true;
        DownloadStatusText = $"Started download {job.DownloadId}.";
        DownloadProgress = 0;
        IsCatalogIntakeOpen = false;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ActivateSelectedModel()
    {
        if (SelectedCatalogModel is null)
        {
            return;
        }

        await ActivateCatalogModel(SelectedCatalogModel);
    }

    [RelayCommand]
    private async Task PreloadSelectedModel()
    {
        if (SelectedCatalogModel is null)
        {
            return;
        }

        await PreloadCatalogModel(SelectedCatalogModel);
    }

    [RelayCommand]
    private async Task ActivateCatalogModel(ModelItemViewModel? model)
    {
        if (model is null)
        {
            return;
        }

        SelectedCatalogModel = model;
        CatalogStatusText = $"Activating {model.Name}...";
        await _services.Library.ActivateModelAsync(model.ModelId, "thinking");
        RefreshCatalog();
        RefreshLoadedInstances();
    }

    [RelayCommand]
    private async Task PreloadCatalogModel(ModelItemViewModel? model)
    {
        if (model is null)
        {
            return;
        }

        SelectedCatalogModel = model;
        CatalogStatusText = $"Preloading {model.Name}...";
        await _services.Library.PreloadModelAsync(model.ModelId);
        RefreshCatalog();
        RefreshLoadedInstances();
    }

    private void RefreshCatalog()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var selectedModelId = SelectedCatalogModel?.ModelId ?? string.Empty;
            var libraryItems = _services.Library.GetLibraryItems().ToDictionary(item => item.ModelId, StringComparer.OrdinalIgnoreCase);
            DownloadedModels.Clear();
            _catalogIndex.Clear();
            foreach (var entry in _services.Catalog.GetEntries())
            {
                var sizeText = entry.FileSizeBytes > 0
                    ? $"{entry.FileSizeBytes / (1024d * 1024d):F1} MB"
                    : "unknown";
                var stateText = libraryItems.TryGetValue(entry.ModelId, out var libraryItem)
                    ? libraryItem.LoadState
                    : entry.Status;
                var model = new ModelItemViewModel(
                    entry.ModelId,
                    entry.DisplayName,
                    sizeText,
                    stateText,
                    entry.LocalPath,
                    libraryItem?.IsPinned ?? false,
                    entry.HealthStatus,
                    entry.RuntimeBackend,
                    libraryItem is null ? string.Empty : string.Join(", ", libraryItem.AssignedEndpoints),
                    entry.IsRunnable,
                    entry.HealthReason,
                    entry.ModelFamily,
                    entry.QuantType,
                    entry.EstimatedMemoryMb,
                    entry.MaxMemoryMb,
                    entry.MaxThreads,
                    entry.ContextSize,
                    entry.AdmissionStatus);
                DownloadedModels.Add(model);
                _catalogIndex[model.ModelId] = model;
            }

            SelectedCatalogModel = DownloadedModels.FirstOrDefault(model => string.Equals(model.ModelId, selectedModelId, StringComparison.OrdinalIgnoreCase));
            CatalogStatusText = $"{DownloadedModels.Count} catalog models | {LoadedInstances.Count} loaded";
        });
    }

    private void RefreshLoadedInstances()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var snapshots = _services.Library.GetLoadedInstances();
            var visibleIds = snapshots.Select(snapshot => snapshot.InstanceId).ToHashSet(StringComparer.OrdinalIgnoreCase);

            for (var index = LoadedInstances.Count - 1; index >= 0; index--)
            {
                var card = LoadedInstances[index];
                if (visibleIds.Contains(card.InstanceId))
                {
                    continue;
                }

                _loadedInstanceIndex.Remove(card.InstanceId);
                LoadedInstances.RemoveAt(index);
            }

            foreach (var snapshot in snapshots)
            {
                if (_loadedInstanceIndex.TryGetValue(snapshot.InstanceId, out var existing))
                {
                    existing.UpdateSnapshot(snapshot);
                    existing.UpdateConsole(snapshot);
                    continue;
                }

                var card = new LoadedInstanceCardViewModel(_services, snapshot);
                _loadedInstanceIndex[snapshot.InstanceId] = card;
                LoadedInstances.Add(card);
            }

            InstanceSettingsStatusText = snapshots.Count == 0
                ? "No loaded instances."
                : $"{snapshots.Count} loaded instance(s).";
            CatalogStatusText = $"{DownloadedModels.Count} catalog models | {snapshots.Count} loaded";
            OnPropertyChanged(nameof(HasLoadedInstances));
            OnPropertyChanged(nameof(NoLoadedInstances));
        });
    }

    private void OnJobUpdated(NexusCentral_Avalonia.Models.DownloadJob job)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ActiveDownloadId = job.DownloadId;
            IsDownloading = job.Status is "queued" or "downloading";
            DownloadProgress = job.ProgressPercent;
            DownloadStatusText = $"{job.Status.ToUpperInvariant()} | {job.BytesDownloaded / (1024d * 1024d):F1} MB"
                + (job.TotalBytes.HasValue ? $" / {job.TotalBytes.Value / (1024d * 1024d):F1} MB" : string.Empty)
                + $" | {job.BytesPerSecond / (1024d * 1024d):F2} MB/s";

            if (job.Status is "completed" or "failed")
            {
                RefreshCatalog();
            }
        });
    }
}
