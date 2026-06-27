using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using NexusCentral_Avalonia.Services;

namespace NexusCentral_Avalonia.Views;

public static class SplitWorkspaceLayoutHelper
{
    public static void Attach(Grid grid, GridSplitter splitter, string tabKey, SplitWorkspaceLayoutService service, int leftColumnIndex = 0, int rightColumnIndex = 2, double fallbackRatio = 0.25)
    {
        void ApplyCurrentRatio()
        {
            var ratio = service.GetRatio(tabKey, fallbackRatio);
            ApplyRatio(grid, leftColumnIndex, rightColumnIndex, ratio);
        }

        void SaveCurrentRatio()
        {
            service.SetRatio(tabKey, GetRatio(grid, leftColumnIndex, rightColumnIndex));
        }

        grid.AttachedToVisualTree += (_, _) => ApplyCurrentRatio();
        grid.SizeChanged += (_, _) => ApplyCurrentRatio();
        splitter.DragCompleted += (_, _) => SaveCurrentRatio();
        splitter.Loaded += (_, _) => ApplyCurrentRatio();
    }

    private static void ApplyRatio(Grid grid, int leftColumnIndex, int rightColumnIndex, double ratio)
    {
        if (grid.ColumnDefinitions.Count <= Math.Max(leftColumnIndex, rightColumnIndex))
        {
            return;
        }

        var available = Math.Max(0, grid.Bounds.Width - GetFixedWidth(grid, leftColumnIndex, rightColumnIndex));
        if (available <= 0)
        {
            return;
        }

        var left = Math.Max(0, available * ratio);
        var right = Math.Max(0, available - left);
        grid.ColumnDefinitions[leftColumnIndex].Width = new GridLength(left, GridUnitType.Pixel);
        grid.ColumnDefinitions[rightColumnIndex].Width = new GridLength(right, GridUnitType.Pixel);
    }

    private static double GetRatio(Grid grid, int leftColumnIndex, int rightColumnIndex)
    {
        if (grid.ColumnDefinitions.Count <= Math.Max(leftColumnIndex, rightColumnIndex))
        {
            return 0.5;
        }

        var left = grid.ColumnDefinitions[leftColumnIndex].ActualWidth;
        var right = grid.ColumnDefinitions[rightColumnIndex].ActualWidth;
        var total = left + right;
        return total <= 0 ? 0.5 : left / total;
    }

    private static double GetFixedWidth(Grid grid, int leftColumnIndex, int rightColumnIndex)
    {
        var fixedWidth = 0d;
        for (var i = 0; i < grid.ColumnDefinitions.Count; i++)
        {
            if (i == leftColumnIndex || i == rightColumnIndex)
            {
                continue;
            }

            fixedWidth += grid.ColumnDefinitions[i].ActualWidth;
        }

        return fixedWidth;
    }
}
