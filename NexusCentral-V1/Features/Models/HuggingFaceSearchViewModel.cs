using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NexusCentral.V1.Features.Models;

public partial class HuggingFaceSearchViewModel : ObservableObject
{
    private readonly IHuggingFaceModelSearchService _service;

    public HuggingFaceSearchViewModel(IHuggingFaceModelSearchService service)
    {
        _service = service;
        _service.StateChanged += Render;
        Render(_service.GetSnapshot());
    }

    public ObservableCollection<HuggingFaceModelSearchItemViewModel> Results { get; } = new();

    [ObservableProperty]
    private HuggingFaceModelSearchItemViewModel? _selectedResult;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private string _statusLabel = "Enter a Hugging Face model name to search.";

    public string ResultsSummaryText => Results.Count == 0
        ? "No Hugging Face search results yet."
        : $"{Results.Count} models found";

    public string SelectedResultTitle => SelectedResult?.Title ?? "No search result selected.";

    public string SelectedResultModelUrl => SelectedResult?.ModelUrl ?? "Model URL: none";

    public string SelectedResultMetaText => SelectedResult?.MetaText ?? "Search results appear here.";

    public string SelectedResultStatsText => SelectedResult?.StatsText ?? "Downloads and likes appear here.";

    public string SelectedResultTagsText => SelectedResult?.TagsText ?? "Tags: none";

    public string SelectedResultVisibilityText => SelectedResult?.VisibilityText ?? "Visibility: unknown";

    [RelayCommand]
    public async Task SearchAsync()
    {
        StatusLabel = string.IsNullOrWhiteSpace(Query)
            ? "Enter a Hugging Face model name to search."
            : $"Searching Hugging Face for '{Query.Trim()}'...";

        await _service.SearchAsync(new HuggingFaceModelSearchRequest(Query, 20));
    }

    [RelayCommand]
    public async Task ClearAsync()
    {
        Query = string.Empty;
        await _service.SearchAsync(new HuggingFaceModelSearchRequest(string.Empty, 20));
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await _service.SearchAsync(new HuggingFaceModelSearchRequest(Query, 20));
    }

    partial void OnSelectedResultChanged(HuggingFaceModelSearchItemViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedResultTitle));
        OnPropertyChanged(nameof(SelectedResultModelUrl));
        OnPropertyChanged(nameof(SelectedResultMetaText));
        OnPropertyChanged(nameof(SelectedResultStatsText));
        OnPropertyChanged(nameof(SelectedResultTagsText));
        OnPropertyChanged(nameof(SelectedResultVisibilityText));
    }

    private void Render(HuggingFaceModelSearchSnapshot snapshot)
    {
        var selectedId = SelectedResult?.Id;
        Results.Clear();
        foreach (var result in snapshot.Results)
        {
            Results.Add(HuggingFaceModelSearchItemViewModel.FromModel(result));
        }

        SelectedResult = Results.FirstOrDefault(result => string.Equals(result.Id, selectedId, System.StringComparison.OrdinalIgnoreCase))
            ?? Results.FirstOrDefault();
        StatusLabel = snapshot.StatusText;

        OnPropertyChanged(nameof(ResultsSummaryText));
        OnPropertyChanged(nameof(SelectedResultTitle));
        OnPropertyChanged(nameof(SelectedResultModelUrl));
        OnPropertyChanged(nameof(SelectedResultMetaText));
        OnPropertyChanged(nameof(SelectedResultStatsText));
        OnPropertyChanged(nameof(SelectedResultTagsText));
        OnPropertyChanged(nameof(SelectedResultVisibilityText));
    }
}

public sealed class HuggingFaceModelSearchItemViewModel
{
    private HuggingFaceModelSearchItemViewModel(HuggingFaceModelSearchEntry entry)
    {
        Id = entry.Id;
        Title = entry.Id;
        PipelineText = string.IsNullOrWhiteSpace(entry.PipelineTag)
            ? "Pipeline: unknown"
            : $"Pipeline: {entry.PipelineTag}";
        LibraryText = string.IsNullOrWhiteSpace(entry.LibraryName)
            ? "Library: unknown"
            : $"Library: {entry.LibraryName}";
        CreatedText = entry.CreatedAtUtc is null
            ? "Created: unknown"
            : $"Created: {entry.CreatedAtUtc.Value.UtcDateTime:yyyy-MM-dd}";
        MetaText = $"{PipelineText} · {LibraryText} · {CreatedText}";
        StatsText = $"Downloads: {entry.Downloads:N0} · Likes: {entry.Likes:N0}";
        VisibilityText = entry.Private ? "Visibility: private" : "Visibility: public";
        TagsText = entry.Tags.Count == 0
            ? "Tags: none"
            : $"Tags: {string.Join(", ", entry.Tags.Take(6))}";
        ModelUrl = $"https://huggingface.co/{entry.Id}";
        DetailText = $"{MetaText} · {StatsText}";
    }

    public string Id { get; }

    public string Title { get; }

    public string PipelineText { get; }

    public string LibraryText { get; }

    public string CreatedText { get; }

    public string MetaText { get; }

    public string StatsText { get; }

    public string VisibilityText { get; }

    public string TagsText { get; }

    public string ModelUrl { get; }

    public string DetailText { get; }

    public static HuggingFaceModelSearchItemViewModel FromModel(HuggingFaceModelSearchEntry entry) => new(entry);
}
