using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace NexusCentral_Avalonia.ViewModels;

public partial class MonitorViewModel : ViewModelBase
{
    private readonly Services.AppServices _services;

    public ObservableCollection<Models.LogEvent> LogEvents { get; } = new();

    [ObservableProperty]
    private string _connectionState = "Disconnected";

    [ObservableProperty]
    private string _apiUrl = "http://127.0.0.1:8000";

    [ObservableProperty]
    private string _statusText = "● DISCONNECTED";

    [ObservableProperty]
    private string _summaryText = "Supervisor: ?";

    [ObservableProperty]
    private string _statusDetail = string.Empty;

    [ObservableProperty]
    private string _connectionColor = "TokyoSecondaryBrush";

    [ObservableProperty]
    private bool _isConnected;

    public MonitorViewModel(Services.AppServices services)
    {
        _services = services;
        
        // Initial load
        foreach (var e in _services.Logs.GetEvents())
        {
            LogEvents.Add(e);
        }

        _services.Logs.LogAppended += e =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                LogEvents.Add(e);
                if (LogEvents.Count > 1000)
                {
                    LogEvents.RemoveAt(0);
                }
            });
        };
    }

    public void UpdateStatus(string statusText, string summaryText, string detail = "")
    {
        StatusText = statusText.ToUpper();
        SummaryText = summaryText;
        StatusDetail = detail;
        IsConnected = statusText.Contains("Online");

        if (statusText.Contains("Online"))
            ConnectionColor = "TokyoSuccessBrush";
        else if (statusText.Contains("Connecting"))
            ConnectionColor = "TokyoWarningBrush";
        else if (statusText.Contains("Error"))
            ConnectionColor = "TokyoErrorBrush";
        else
            ConnectionColor = "TokyoSecondaryBrush";
    }
}
