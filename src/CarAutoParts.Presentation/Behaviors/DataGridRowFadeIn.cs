using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace CarAutoParts.Presentation.Behaviors;

public static class DataGridRowFadeIn
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(DataGridRowFadeIn),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid || e.NewValue is not true)
            return;

        grid.LoadingRow -= OnLoadingRow;
        grid.LoadingRow += OnLoadingRow;
    }

    private static void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        e.Row.Opacity = 0;
        var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        e.Row.BeginAnimation(UIElement.OpacityProperty, animation);
    }
}
