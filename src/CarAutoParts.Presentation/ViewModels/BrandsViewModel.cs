using CarAutoParts.Application.DTOs.Products;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarAutoParts.Presentation.ViewModels;

public partial class BrandsViewModel : EntityModuleViewModelBase<BrandDto>
{
    private readonly IBrandService _service;
    private int? _editingId;

    public BrandsViewModel(
        IBrandService service,
        IExceptionHandler exceptionHandler,
        ISnackbarService snackbarService,
        IDialogService dialogService)
        : base("Brands", "Manage product brands", exceptionHandler, snackbarService, dialogService)
    {
        _service = service;
    }

    [ObservableProperty]
    private BrandDto? _selectedItem;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string? _editLogoUrl;

    [ObservableProperty]
    private bool _editIsActive = true;

    protected override async Task LoadItemsAsync()
    {
        var items = await _service.GetAllAsync();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(SearchText) ||
                item.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            {
                Items.Add(item);
            }
        }
    }

    [RelayCommand]
    private void New()
    {
        _editingId = null;
        EditName = string.Empty;
        EditLogoUrl = null;
        EditIsActive = true;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void Edit(BrandDto? item)
    {
        if (item is null)
            return;

        _editingId = item.Id;
        EditName = item.Name;
        EditLogoUrl = item.LogoUrl;
        EditIsActive = item.IsActive;
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

        var dto = new BrandDto(_editingId ?? 0, EditName.Trim(), EditLogoUrl, EditIsActive);

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
                SnackbarService.Show("Brand updated.");
            }
            else
            {
                var result = await _service.CreateAsync(dto);
                if (!result.Succeeded)
                {
                    SnackbarService.Show(result.Error ?? "Create failed.");
                    return;
                }
                SnackbarService.Show("Brand created.");
            }

            IsEditorOpen = false;
            await RefreshAsync();
        }, "Save brand");
    }

    [RelayCommand]
    private void CancelEdit() => IsEditorOpen = false;

    [RelayCommand]
    private async Task DeleteAsync(BrandDto? item)
    {
        if (item is null)
            return;

        var confirmed = await DialogService.ConfirmAsync("Delete brand", $"Delete {item.Name}?");
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
            SnackbarService.Show("Brand deleted.");
            await RefreshAsync();
        }, "Delete brand");
    }
}
