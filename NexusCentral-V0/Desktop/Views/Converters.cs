using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace NexusCentral_Avalonia.Views;

public class RoleToAlignmentConverter : IValueConverter
{
    public static readonly RoleToAlignmentConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string role)
        {
            return role == "user" ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }
        return HorizontalAlignment.Left;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class ResourceKeyToBrushConverter : IValueConverter
{
    public static readonly ResourceKeyToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value as string;
        if (!string.IsNullOrWhiteSpace(key) && Application.Current?.TryFindResource(key, out var resource) == true)
        {
            return resource;
        }

        return Application.Current?.FindResource("TokyoAccentBrush") ?? Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class RoleToBackgroundConverter : IMultiValueConverter
{
    public static readonly RoleToBackgroundConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count > 0 && values[0] is string role)
        {
            if (role == "user") return Application.Current!.FindResource("TokyoAccentBrush");
            if (role == "error") return Application.Current!.FindResource("TokyoErrorBrush");
            return Application.Current!.FindResource("TokyoSurfaceBrush");
        }
        return Brushes.Transparent;
    }
}

public class RoleToForegroundConverter : IValueConverter
{
    public static readonly RoleToForegroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string role)
        {
            return role == "user" ? Brushes.White : Application.Current!.FindResource("TokyoFGBrush");
        }
        return Brushes.White;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class RoleToAccentBrushConverter : IValueConverter
{
    public static readonly RoleToAccentBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string role)
        {
            if (string.Equals(role, "user", StringComparison.OrdinalIgnoreCase))
            {
                return Application.Current!.FindResource("TokyoAccentBrush");
            }

            if (string.Equals(role, "error", StringComparison.OrdinalIgnoreCase))
            {
                return Application.Current!.FindResource("TokyoErrorBrush");
            }
        }

        return Application.Current!.FindResource("TokyoDividerStrongBrush");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class RoleToRowBackgroundConverter : IValueConverter
{
    public static readonly RoleToRowBackgroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
public class StringToUpperConverter : IValueConverter
{
    public static readonly StringToUpperConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString()?.ToUpper();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class BooleanToStringConverter : IValueConverter
{
    public static readonly BooleanToStringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && parameter is string p)
        {
            var parts = p.Split('|');
            if (parts.Length == 2)
            {
                return b ? parts[0] : parts[1];
            }
        }
        return value?.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class BooleanToResourceConverter : IValueConverter
{
    public static readonly BooleanToResourceConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string resourceNames)
        {
            return Brushes.Transparent;
        }

        var parts = resourceNames.Split('|');
        if (parts.Length != 2)
        {
            return Brushes.Transparent;
        }

        var resourceName = value is bool flag && flag ? parts[0] : parts[1];
        if (Application.Current!.TryGetResource(resourceName, out var resource))
        {
            return resource;
        }

        return string.Equals(resourceName, "Transparent", StringComparison.OrdinalIgnoreCase)
            ? Brushes.Transparent
            : Application.Current!.FindResource("TokyoSecondaryBrush");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class BooleanToThicknessConverter : IValueConverter
{
    public static readonly BooleanToThicknessConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string thicknessValues)
        {
            return new Thickness(0);
        }

        var parts = thicknessValues.Split('|');
        if (parts.Length != 2)
        {
            return new Thickness(0);
        }

        return Thickness.Parse(value is bool flag && flag ? parts[0] : parts[1]);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class InverseBooleanConverter : IValueConverter
{
    public static readonly InverseBooleanConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool flag ? !flag : false;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class StringHasValueConverter : IValueConverter
{
    public static readonly StringHasValueConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !string.IsNullOrWhiteSpace(value?.ToString());

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class SelectedBackgroundConverter : IValueConverter
{
    public static readonly SelectedBackgroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isSelected = value is bool b && b;
        return isSelected 
            ? Application.Current!.FindResource("TokyoSurfaceHighlightBrush") 
            : Application.Current!.FindResource("TokyoSurfaceBrush");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class SelectedBorderConverter : IValueConverter
{
    public static readonly SelectedBorderConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isSelected = value is bool b && b;
        return isSelected 
            ? Application.Current!.FindResource("TokyoAccentBrush") 
            : Application.Current!.FindResource("TokyoSecondaryBrush");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class ResourceToBrushConverter : IValueConverter
{
    public static readonly ResourceToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string resourceName && !string.IsNullOrEmpty(resourceName))
        {
            if (Application.Current!.TryGetResource(resourceName, out var resource) && resource is IBrush brush)
            {
                return brush;
            }
        }
        return Application.Current!.FindResource("TokyoSecondaryBrush");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class TimestampToDateTimeConverter : IValueConverter
{
    public static readonly TimestampToDateTimeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long ms)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime;
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class LogLevelToBrushConverter : IValueConverter
{
    public static readonly LogLevelToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string level)
        {
            return level.ToLower() switch
            {
                "error" => Application.Current!.FindResource("TokyoErrorBrush"),
                "warning" => Application.Current!.FindResource("TokyoWarningBrush"),
                "info" => Application.Current!.FindResource("TokyoAccentBrush"),
                _ => Application.Current!.FindResource("TokyoSecondaryBrush")
            };
        }
        return Application.Current!.FindResource("TokyoSecondaryBrush");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class StringNotEqualsConverter : IValueConverter
{
    public static readonly StringNotEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class StringEqualsConverter : IValueConverter
{
    public static readonly StringEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
