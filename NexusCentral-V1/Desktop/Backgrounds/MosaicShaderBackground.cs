using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NexusCentral.V1.Desktop.Backgrounds;

public sealed class MosaicShaderBackground : Control
{
    private const double MaxParallaxX = 18.0;
    private const double MaxParallaxY = 12.0;
    private double _pointerX;
    private double _pointerY;

    public MosaicBackgroundOptions Options { get; init; } = MosaicBackgroundOptions.Default;

    public void SetPointerOffset(double normalizedX, double normalizedY)
    {
        _pointerX = Math.Clamp(normalizedX, -1.0, 1.0);
        _pointerY = Math.Clamp(normalizedY, -1.0, 1.0);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var rect = new Rect(Bounds.Size);
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        DrawBase(context, rect);
        DrawMosaicRoutes(context, rect);
        DrawVignette(context, rect);
    }

    private void DrawBase(DrawingContext context, Rect rect)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Options.BaseTop, 0),
                new GradientStop(Color.Parse("#082B27"), 0.42),
                new GradientStop(Options.BaseBottom, 1)
            ]
        };

        context.DrawRectangle(brush, null, rect);
    }

    private void DrawMosaicRoutes(DrawingContext context, Rect rect)
    {
        var width = rect.Width;
        var height = rect.Height;
        var offsetX = -_pointerX * MaxParallaxX;
        var offsetY = -_pointerY * MaxParallaxY;
        var lineBrush = new SolidColorBrush(Options.MosaicLine, 0.24);
        var accentBrush = new SolidColorBrush(Options.MosaicAccent, 0.15);
        var dimBrush = new SolidColorBrush(Options.MosaicLine, 0.10);

        for (var i = 0; i < 9; i++)
        {
            var t = i / 8.0;
            var start = new Point(-width * 0.08 + offsetX * 0.35, height * (0.12 + t * 0.74) + offsetY * 0.2);
            var end = new Point(width * (0.72 + t * 0.3) + offsetX, height * (0.02 + t * 0.92) + offsetY);
            context.DrawLine(new Pen(dimBrush, 1), start, end);
        }

        DrawBrokenRoute(
            context,
            new Pen(lineBrush, 1.15),
            new Point(width * 0.62 + offsetX, -20 + offsetY),
            new Point(width * 0.76 + offsetX * 0.8, height * 0.24 + offsetY),
            new Point(width * 0.93 + offsetX * 0.5, height * 0.32 + offsetY * 0.5),
            new Point(width + 20, height * 0.42 + offsetY * 0.3));

        DrawBrokenRoute(
            context,
            new Pen(accentBrush, 1.35),
            new Point(width * 0.08 + offsetX * 0.5, height + 20),
            new Point(width * 0.3 + offsetX, height * 0.78 + offsetY),
            new Point(width * 0.52 + offsetX * 0.7, height * 0.74 + offsetY * 0.4),
            new Point(width * 0.74 + offsetX * 0.25, height * 0.58 + offsetY * 0.2));

        for (var i = 0; i < 7; i++)
        {
            var x = width * (0.78 + i * 0.035) + offsetX * (0.2 + i * 0.06);
            context.DrawLine(
                new Pen(new SolidColorBrush(Options.MosaicAccent, 0.10), 1),
                new Point(x, height * 0.62 + offsetY * 0.2),
                new Point(x + width * 0.08, height + 10));
        }
    }

    private static void DrawBrokenRoute(DrawingContext context, Pen pen, params Point[] points)
    {
        for (var i = 0; i < points.Length - 1; i++)
        {
            context.DrawLine(pen, points[i], points[i + 1]);
        }
    }

    private void DrawVignette(DrawingContext context, Rect rect)
    {
        var brush = new RadialGradientBrush
        {
            Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(0.78, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.78, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Color.Parse("#00000000"), 0),
                new GradientStop(Color.Parse("#22020708"), 0.66),
                new GradientStop(Options.Vignette, 1)
            ]
        };

        context.DrawRectangle(brush, null, rect);
    }
}
