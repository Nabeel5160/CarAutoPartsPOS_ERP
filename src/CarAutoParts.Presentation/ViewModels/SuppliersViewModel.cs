using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Partners;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarAutoParts.Presentation.ViewModels;

public partial class SuppliersViewModel : EntityModuleViewModelBase<SupplierDto>
{
    private readonly ISupplierService _service;
    private readonly INavigationState _navigationState;
    private int? _editingId;

    public SuppliersViewModel(
        ISupplierService service,
        INavigationState navigationState,
        IExceptionHandler exceptionHandler,
        ISnackbarService snackbarService,
        IDialogService dialogService)
        : base("Suppliers", "Manage vendor accounts", exceptionHandler, snackbarService, dialogService)
    {
        _service = service;
        _navigationState = navigationState;
        ShowPaging = true;
    }

    public override async Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_navigationState.SupplierSearch))
        {
            SearchText = _navigationState.SupplierSearch;
            _navigationState.SupplierSearch = null;
        }

        await base.InitializeAsync();
    }

    [ObservableProperty]
    private SupplierDto? _selectedItem;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string? _editCompany;

    [ObservableProperty]
    private string? _editCity;

    [ObservableProperty]
    private string? _editPhone;

    [ObservableProperty]
    private string? _editEmail;

    [ObservableProperty]
    private bool _editIsActive = true;

    protected override async Task LoadItemsAsync()
    {
        var result = await _service.GetSuppliersAsync(new QuerySpec
        {
            Search = SearchText,
            Page = CurrentPage,
            PageSize = 25
        });

        foreach (var item in result.Items)
            Items.Add(item);

        TotalPages = Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)result.PageSize));
    }

    [RelayCommand]
    private void New()
    {
        _editingId = null;
        EditName = string.Empty;
        EditCompany = null;
        EditCity = null;
        EditPhone = null;
        EditEmail = null;
        EditIsActive = true;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void Edit(SupplierDto? item)
    {
        if (item is null)
            return;

        _editingId = item.Id;
        EditName = item.Name;
        EditCompany = item.Company;
        EditCity = item.City;
        EditPhone = item.Phone;
        EditEmail = item.Email;
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

        var dto = new SupplierDto(
            _editingId ?? 0,
            EditName.Trim(),
            EditCompany,
            EditCity,
            string.IsNullOrWhiteSpace(EditPhone) ? null : EditPhone.Trim(),
            string.IsNullOrWhiteSpace(EditEmail) ? null : EditEmail.Trim(),
            0,
            EditIsActive);

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
                SnackbarService.Show("Supplier updated.");
            }
            else
            {
                var result = await _service.CreateAsync(dto);
                if (!result.Succeeded)
                {
                    SnackbarService.Show(result.Error ?? "Create failed.");
                    return;
                }
                SnackbarService.Show("Supplier created.");
            }

            IsEditorOpen = false;
            await RefreshAsync();
        }, "Save supplier");
    }

    [RelayCommand]
    private void CancelEdit() => IsEditorOpen = false;

    [RelayCommand]
    private async Task DeleteAsync(SupplierDto? item)
    {
        if (item is null)
            return;

        var confirmed = await DialogService.ConfirmAsync("Delete supplier", $"Delete {item.Name}?");
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
            SnackbarService.Show("Supplier deleted.");
            await RefreshAsync();
        }, "Delete supplier");
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await RefreshAsync();
        }
    }
}
