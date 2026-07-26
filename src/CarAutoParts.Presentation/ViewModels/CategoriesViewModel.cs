using System.Collections.ObjectModel;
using CarAutoParts.Application.DTOs.Products;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarAutoParts.Presentation.ViewModels;

public record CategoryListItem(
    int Id,
    string Name,
    string? ParentName,
    int ChildCount,
    string? Description);

public partial class CategoriesViewModel : EntityModuleViewModelBase<CategoryListItem>
{
    private readonly ICategoryService _service;
    private int? _editingId;

    public CategoriesViewModel(
        ICategoryService service,
        IExceptionHandler exceptionHandler,
        ISnackbarService snackbarService,
        IDialogService dialogService)
        : base("Categories", "Organize product hierarchy", exceptionHandler, snackbarService, dialogService)
    {
        _service = service;
    }

    public ObservableCollection<CategoryDto> ParentOptions { get; } = new();

    [ObservableProperty]
    private CategoryListItem? _selectedItem;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string? _editDescription;

    [ObservableProperty]
    private int? _editParentId;

    protected override async Task LoadItemsAsync()
    {
        var tree = await _service.GetTreeAsync();
        ParentOptions.Clear();
        foreach (var node in FlattenForOptions(tree))
            ParentOptions.Add(node);

        foreach (var item in FlattenForGrid(tree, null))
            Items.Add(item);
    }

    [RelayCommand]
    private void New()
    {
        _editingId = null;
        EditName = string.Empty;
        EditDescription = null;
        EditParentId = null;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void Edit(CategoryListItem? item)
    {
        if (item is null)
            return;

        _editingId = item.Id;
        EditName = item.Name;
        EditDescription = item.Description;
        EditParentId = ParentOptions.FirstOrDefault(p => p.Name == item.ParentName)?.Id;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName))
        {
            SnackbarService.Show("Name is required.");
            return;
        }

        var dto = new CategoryDto(
            _editingId ?? 0,
            EditName.Trim(),
            string.IsNullOrWhiteSpace(EditDescription) ? null : EditDescription.Trim(),
            null,
            EditParentId,
            Array.Empty<CategoryDto>());

        await ExceptionHandler.ExecuteAsync(async () =>
        {
            if (_editingId is int id)
            {
                var result = await _service.UpdateAsync(id, dto);
                if (!result.Succeeded)
                {
                    SnackbarService.Show(result.Error ?? "Update failed.");
                    return;
                }
                SnackbarService.Show("Category updated.");
            }
            else
            {
                var result = await _service.CreateAsync(dto);
                if (!result.Succeeded)
                {
                    SnackbarService.Show(result.Error ?? "Create failed.");
                    return;
                }
                SnackbarService.Show("Category created.");
            }

            IsEditorOpen = false;
            await RefreshAsync();
        }, "Save category");
    }

    [RelayCommand]
    private void CancelEdit() => IsEditorOpen = false;

    [RelayCommand]
    private async Task DeleteAsync(CategoryListItem? item)
    {
        if (item is null)
            return;

        var confirmed = await DialogService.ConfirmAsync("Delete category", $"Delete {item.Name}?");
        if (!confirmed)
            return;

        await ExceptionHandler.ExecuteAsync(async () =>
        {
            var result = await _service.DeleteAsync(item.Id);
            if (!result.Succeeded)
            {
                SnackbarService.Show(result.Error ?? "Delete failed.");
                return;
            }
            SnackbarService.Show("Category deleted.");
            await RefreshAsync();
        }, "Delete category");
    }

    private static IEnumerable<CategoryListItem> FlattenForGrid(
        IReadOnlyList<CategoryDto> nodes,
        string? parentName)
    {
        foreach (var node in nodes)
        {
            yield return new CategoryListItem(
                node.Id,
                node.Name,
                parentName,
                node.Children.Count,
                node.Description);

            foreach (var child in FlattenForGrid(node.Children, node.Name))
                yield return child;
        }
    }

    private static IEnumerable<CategoryDto> FlattenForOptions(IReadOnlyList<CategoryDto> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in FlattenForOptions(node.Children))
                yield return child;
        }
    }
}
