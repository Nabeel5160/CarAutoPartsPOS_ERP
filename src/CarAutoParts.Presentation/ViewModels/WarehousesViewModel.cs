using CarAutoParts.Application.DTOs.Products;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarAutoParts.Presentation.ViewModels;

public partial class WarehousesViewModel : EntityModuleViewModelBase<WarehouseDto>
{
    private readonly IWarehouseService _service;
    private int? _editingId;

    public WarehousesViewModel(
        IWarehouseService service,
        IExceptionHandler exceptionHandler,
        ISnackbarService snackbarService,
        IDialogService dialogService)
        : base("Warehouses", "Manage storage locations", exceptionHandler, snackbarService, dialogService)
    {
        _service = service;
    }

    [ObservableProperty]
    private WarehouseDto? _selectedItem;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string? _editAddress;

    [ObservableProperty]
    private string? _editCity;

    [ObservableProperty]
    private string? _editContactPerson;

    [ObservableProperty]
    private string? _editPhoneNumber;

    [ObservableProperty]
    private bool _editIsDefault;

    protected override async Task LoadItemsAsync()
    {
        var items = await _service.GetAllAsync();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(SearchText) ||
                item.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (item.City?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false))
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
        EditAddress = null;
        EditCity = null;
        EditContactPerson = null;
        EditPhoneNumber = null;
        EditIsDefault = false;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void Edit(WarehouseDto? item)
    {
        if (item is null)
            return;

        _editingId = item.Id;
        EditName = item.Name;
        EditAddress = item.Address;
        EditCity = item.City;
        EditContactPerson = item.ContactPerson;
        EditPhoneNumber = item.PhoneNumber;
        EditIsDefault = item.IsDefault;
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

        var dto = new WarehouseDto(
            _editingId ?? 0,
            EditName.Trim(),
            EditAddress,
            EditCity,
            EditContactPerson,
            EditPhoneNumber,
            EditIsDefault);

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
                SnackbarService.Show("Warehouse updated.");
            }
            else
            {
                var result = await _service.CreateAsync(dto);
                if (!result.Succeeded)
                {
                    SnackbarService.Show(result.Error ?? "Create failed.");
                    return;
                }
                SnackbarService.Show("Warehouse created.");
            }

            IsEditorOpen = false;
            await RefreshAsync();
        }, "Save warehouse");
    }

    [RelayCommand]
    private void CancelEdit() => IsEditorOpen = false;

    [RelayCommand]
    private async Task DeleteAsync(WarehouseDto? item)
    {
        if (item is null)
            return;

        var confirmed = await DialogService.ConfirmAsync("Delete warehouse", $"Delete {item.Name}?");
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
            SnackbarService.Show("Warehouse deleted.");
            await RefreshAsync();
        }, "Delete warehouse");
    }
}
