using Avalonia.Media;

namespace NexusCentral.V1.Desktop.Backgrounds;

public sealed record MosaicBackgroundOptions(
    Color BaseTop,
    Color BaseBottom,
    Color MosaicLine,
    Color MosaicAccent,
    Color Vignette)
{
    public static MosaicBackgroundOptions Default { get; } = new(
        Color.Parse("#061B1D"),
        Color.Parse("#020B12"),
        Color.Parse("#266F5B"),
        Color.Parse("#8CCFB1"),
        Color.Parse("#CC020607"));
}
