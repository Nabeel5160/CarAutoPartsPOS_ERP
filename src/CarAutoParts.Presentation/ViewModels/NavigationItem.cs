using CommunityToolkit.Mvvm.ComponentModel;

namespace CarAutoParts.Presentation.ViewModels;

public partial class NavigationItem : ObservableObject
{
    public required string Title { get; init; }
    public required string Icon { get; init; }
    public string? PermissionCode { get; init; }
    public required Type ViewModelType { get; init; }

    [ObservableProperty]
    private bool _isSelected;
}
