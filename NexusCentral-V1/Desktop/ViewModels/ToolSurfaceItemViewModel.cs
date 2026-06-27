namespace NexusCentral.V3.Desktop.ViewModels;

public sealed class ToolSurfaceItemViewModel
{
    public ToolSurfaceItemViewModel(string name, string route, string scope, string status, string safety)
    {
        Name = name;
        Route = route;
        Scope = scope;
        Status = status;
        Safety = safety;
    }

    public string Name { get; }

    public string Route { get; }

    public string Scope { get; }

    public string Status { get; }

    public string Safety { get; }
}
