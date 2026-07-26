using System.Collections.ObjectModel;
using CarAutoParts.Application.DTOs.Inventory;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarAutoParts.Presentation.ViewModels;

public record WarehouseFilterOption(int? Id, string Name);

public partial class InventoryViewModel : EntityModuleViewModelBase<InventoryItemDto>
{
    private readonly IInventoryService _service;
    private readonly IWarehouseService _warehouseService;
    private readonly INavigationState _navigationState;

    public InventoryViewModel(
        IInventoryService service,
        IWarehouseService warehouseService,
        INavigationState navigationState,
        IExceptionHandler exceptionHandler,
        ISnackbarService snackbarService,
        IDialogService dialogService)
        : base("Inventory", "Stock levels by warehouse", exceptionHandler, snackbarService, dialogService)
    {
        _service = service;
        _warehouseService = warehouseService;
        _navigationState = navigationState;
        PrimaryActionText = "Adjust Stock";
        ShowPaging = true;
    }

    public ObservableCollection<WarehouseFilterOption> WarehouseOptions { get; } = new();

    [ObservableProperty]
    private InventoryItemDto? _selectedItem;

    [ObservableProperty]
    private WarehouseFilterOption? _selectedWarehouse;

    [ObservableProperty]
    private bool _lowStockOnly;

    [ObservableProperty]
    private decimal _editQuantityDelta;

    [ObservableProperty]
    private string? _editReason;

    [ObservableProperty]
    private string _adjustProductLabel = string.Empty;

    [ObservableProperty]
    private string _adjustWarehouseLabel = string.Empty;

    public override async Task InitializeAsync()
    {
        IsModuleReady = false;
        SuppressFilterRefresh = true;
        try
        {
            if (_navigationState.InventoryLowStockOnly)
            {
                LowStockOnly = true;
                _navigationState.InventoryLowStockOnly = false;
                CurrentPage = 1;
            }

            await RefreshAsync();
        }
        finally
        {
            SuppressFilterRefresh = false;
            IsModuleReady = true;
        }
    }

    protected override async Task LoadItemsAsync()
    {
        if (WarehouseOptions.Count == 0)
        {
            WarehouseOptions.Add(new WarehouseFilterOption(null, "All warehouses"));
            var warehouses = await _warehouseService.GetAllAsync();
            foreach (var warehouse in warehouses)
                WarehouseOptions.Add(new WarehouseFilterOption(warehouse.Id, warehouse.Name));

            SelectedWarehouse ??= WarehouseOptions[0];
        }

        var result = await _service.GetInventoryAsync(new InventoryQueryDto
        {
            Search = SearchText,
            WarehouseId = SelectedWarehouse?.Id,
            LowStockOnly = LowStockOnly,
            Page = CurrentPage,
            PageSize = 25
        });

        foreach (var item in result.Items)
            Items.Add(item);

        TotalPages = Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)result.PageSize));
    }

    partial void OnLowStockOnlyChanged(bool value) => _ = SearchAsync();

    partial void OnSelectedWarehouseChanged(WarehouseFilterOption? value)
    {
        if (value is not null)
            _ = SearchAsync();
    }

    [RelayCommand]
    private void New()
    {
        if (SelectedItem is null)
        {
            SnackbarService.Show("Select an inventory row to adjust.");
            return;
        }

        OpenAdjustEditor(SelectedItem);
    }

    [RelayCommand]
    private void AdjustStock(InventoryItemDto? item)
    {
        if (item is null)
            return;

        SelectedItem = item;
        OpenAdjustEditor(item);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedItem is null)
            return;

        if (EditQuantityDelta == 0)
        {
            SnackbarService.Show("Quantity change cannot be zero.");
            return;
        }

        var dto = new StockAdjustmentDto(
            SelectedItem.ProductId,
            SelectedItem.WarehouseId,
            EditQuantityDelta,
            string.IsNullOrWhiteSpace(EditReason) ? null : EditReason.Trim());

        await ExceptionHandler.ExecuteAsync(async () =>
        {
            var result = await _service.AdjustStockAsync(dto);
            if (!result.Succeeded)
            {
                SnackbarService.Show(result.Error ?? "Adjustment failed.");
                return;
            }

            SnackbarService.Show("Stock adjusted.");
            IsEditorOpen = false;
            await RefreshAsync();
        }, "Adjust stock");
    }

    [RelayCommand]
    private void CancelEdit() => IsEditorOpen = false;

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

    private void OpenAdjustEditor(InventoryItemDto item)
    {
        AdjustProductLabel = $"{item.ProductName} ({item.Sku})";
        AdjustWarehouseLabel = item.WarehouseName;
        EditQuantityDelta = 0;
        EditReason = null;
        IsEditorOpen = true;
    }
}
