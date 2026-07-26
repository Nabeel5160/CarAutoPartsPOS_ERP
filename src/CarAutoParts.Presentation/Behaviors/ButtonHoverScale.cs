using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CarAutoParts.Presentation.Behaviors;

public static class ButtonHoverScale
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(ButtonHoverScale),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Button button || e.NewValue is not true)
            return;

        button.RenderTransformOrigin = new Point(0.5, 0.5);
        button.RenderTransform = new ScaleTransform(1, 1);
        button.MouseEnter += OnMouseEnter;
        button.MouseLeave += OnMouseLeave;
    }

    private static void OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Button button || button.RenderTransform is not ScaleTransform scale)
            return;

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, CreateAnimation(1.03));
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, CreateAnimation(1.03));
    }

    private static void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not Button button || button.RenderTransform is not ScaleTransform scale)
            return;

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, CreateAnimation(1.0));
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, CreateAnimation(1.0));
    }

    private static DoubleAnimation CreateAnimation(double to) => new(to, TimeSpan.FromMilliseconds(120))
    {
        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
    };
}
