using System.Windows;
using System.Windows.Controls;

namespace CarAutoParts.Presentation.Views;

public partial class EntityModuleView : UserControl
{
    public static readonly DependencyProperty MainContentProperty =
        DependencyProperty.Register(nameof(MainContent), typeof(object), typeof(EntityModuleView));

    public static readonly DependencyProperty EditorContentProperty =
        DependencyProperty.Register(nameof(EditorContent), typeof(object), typeof(EntityModuleView));

    public object? MainContent
    {
        get => GetValue(MainContentProperty);
        set => SetValue(MainContentProperty, value);
    }

    public object? EditorContent
    {
        get => GetValue(EditorContentProperty);
        set => SetValue(EditorContentProperty, value);
    }

    public EntityModuleView() => InitializeComponent();
}
