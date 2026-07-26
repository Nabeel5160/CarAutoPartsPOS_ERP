using System.Globalization;
using System.Windows;
using System.Windows.Data;
using CarAutoParts.Application.Interfaces;

namespace CarAutoParts.Presentation.Converters;

public class PermissionToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string permissionCode || string.IsNullOrWhiteSpace(permissionCode))
            return Visibility.Visible;

        var userService = App.Services?.GetService(typeof(ICurrentUserService)) as ICurrentUserService;
        return userService?.HasPermission(permissionCode) == true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
