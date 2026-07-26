using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Partners;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Enums;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarAutoParts.Presentation.ViewModels;

public partial class CustomersViewModel : EntityModuleViewModelBase<CustomerDto>
{
    private readonly ICustomerService _service;
    private readonly INavigationState _navigationState;
    private int? _editingId;

    public CustomersViewModel(
        ICustomerService service,
        INavigationState navigationState,
        IExceptionHandler exceptionHandler,
        ISnackbarService snackbarService,
        IDialogService dialogService)
        : base("Customers", "Manage customer accounts and credit", exceptionHandler, snackbarService, dialogService)
    {
        _service = service;
        _navigationState = navigationState;
        ShowPaging = true;
    }

    public Array CustomerTypes { get; } = Enum.GetValues(typeof(CustomerType));

    public override async Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_navigationState.CustomerSearch))
        {
            SearchText = _navigationState.CustomerSearch;
            _navigationState.CustomerSearch = null;
        }

        await base.InitializeAsync();
    }

    [ObservableProperty]
    private CustomerDto? _selectedItem;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private CustomerType _editCustomerType = CustomerType.WalkIn;

    [ObservableProperty]
    private string? _editPhone;

    [ObservableProperty]
    private string? _editEmail;

    [ObservableProperty]
    private decimal _editCreditLimit;

    [ObservableProperty]
    private bool _editIsActive = true;

    protected override async Task LoadItemsAsync()
    {
        var result = await _service.GetCustomersAsync(new QuerySpec
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
        EditCustomerType = CustomerType.WalkIn;
        EditPhone = null;
        EditEmail = null;
        EditCreditLimit = 0;
        EditIsActive = true;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void Edit(CustomerDto? item)
    {
        if (item is null)
            return;

        _editingId = item.Id;
        EditName = item.Name;
        EditCustomerType = item.CustomerType;
        EditPhone = item.Phone;
        EditEmail = item.Email;
        EditCreditLimit = item.CreditLimit;
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

        var dto = new CustomerDto(
            _editingId ?? 0,
            EditName.Trim(),
            EditCustomerType,
            string.IsNullOrWhiteSpace(EditPhone) ? null : EditPhone.Trim(),
            string.IsNullOrWhiteSpace(EditEmail) ? null : EditEmail.Trim(),
            0,
            EditCreditLimit,
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
                SnackbarService.Show("Customer updated.");
            }
            else
            {
                var result = await _service.CreateAsync(dto);
                if (!result.Succeeded)
                {
                    SnackbarService.Show(result.Error ?? "Create failed.");
                    return;
                }
                SnackbarService.Show("Customer created.");
            }

            IsEditorOpen = false;
            await RefreshAsync();
        }, "Save customer");
    }

    [RelayCommand]
    private void CancelEdit() => IsEditorOpen = false;

    [RelayCommand]
    private async Task DeleteAsync(CustomerDto? item)
    {
        if (item is null)
            return;

        var confirmed = await DialogService.ConfirmAsync("Delete customer", $"Delete {item.Name}?");
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
            SnackbarService.Show("Customer deleted.");
            await RefreshAsync();
        }, "Delete customer");
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
