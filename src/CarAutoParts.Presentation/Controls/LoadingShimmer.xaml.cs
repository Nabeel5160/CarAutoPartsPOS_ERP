using System.Windows;
using System.Windows.Controls;

namespace CarAutoParts.Presentation.Controls;

public partial class LoadingShimmer : UserControl
{
    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(LoadingShimmer));

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public LoadingShimmer() => InitializeComponent();
}
