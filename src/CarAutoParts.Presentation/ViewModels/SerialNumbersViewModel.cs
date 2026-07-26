using System.Collections.ObjectModel;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Inventory;
using CarAutoParts.Application.DTOs.Products;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarAutoParts.Presentation.ViewModels;

public partial class SerialNumbersViewModel : EntityModuleViewModelBase<SerialNumberDto>
{
    private readonly ISerialNumberService _service;
    private readonly IProductService _productService;
    private readonly IWarehouseService _warehouseService;

    public SerialNumbersViewModel(
        ISerialNumberService service,
        IProductService productService,
        IWarehouseService warehouseService,
        IExceptionHandler exceptionHandler,
        ISnackbarService snackbarService,
        IDialogService dialogService)
        : base("Serial Numbers", "Track serialized product inventory", exceptionHandler, snackbarService, dialogService)
    {
        _service = service;
        _productService = productService;
        _warehouseService = warehouseService;
        ShowPaging = true;
        PrimaryActionText = "Register Serial";
    }

    public ObservableCollection<ProductListDto> Products { get; } = new();
    public ObservableCollection<WarehouseDto> Warehouses { get; } = new();

    [ObservableProperty]
    private SerialNumberDto? _selectedItem;

    [ObservableProperty]
    private ProductListDto? _registerProduct;

    [ObservableProperty]
    private WarehouseDto? _registerWarehouse;

    [ObservableProperty]
    private string _registerSerial = string.Empty;

    protected override async Task LoadItemsAsync()
    {
        var result = await _service.GetSerialNumbersAsync(new QuerySpec
        {
            Search = SearchText,
            Page = CurrentPage,
            PageSize = 25
        });

        foreach (var item in result.Items)
            Items.Add(item);

        TotalPages = Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)result.PageSize));
    }

    private async Task LoadLookupsAsync()
    {
        if (Products.Count == 0)
        {
            var products = await _productService.GetProductsAsync(new ProductQueryDto { IsActive = true, PageSize = 200 });
            foreach (var p in products.Items)
                Products.Add(p);
        }

        if (Warehouses.Count == 0)
        {
            foreach (var w in await _warehouseService.GetAllAsync())
                Warehouses.Add(w);
        }
    }

    [RelayCommand]
    private async Task NewAsync()
    {
        await ExceptionHandler.ExecuteAsync(async () =>
        {
            await LoadLookupsAsync();
            RegisterSerial = string.Empty;
            RegisterProduct = Products.FirstOrDefault();
            RegisterWarehouse = Warehouses.FirstOrDefault(w => w.IsDefault) ?? Warehouses.FirstOrDefault();
            IsEditorOpen = true;
        }, "Register serial");
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (RegisterProduct is null)
        {
            SnackbarService.Show("Select a product.");
            return;
        }

        if (RegisterWarehouse is null)
        {
            SnackbarService.Show("Select a warehouse.");
            return;
        }

        if (string.IsNullOrWhiteSpace(RegisterSerial))
        {
            SnackbarService.Show("Enter a serial number.");
            return;
        }

        await ExceptionHandler.ExecuteAsync(async () =>
        {
            var result = await _service.RegisterSerialAsync(
                RegisterProduct.Id,
                RegisterSerial.Trim(),
                RegisterWarehouse.Id);

            if (!result.Succeeded)
            {
                SnackbarService.Show(result.Error ?? "Registration failed.");
                return;
            }

            SnackbarService.Show("Serial number registered.");
            IsEditorOpen = false;
            await RefreshAsync();
        }, "Register serial");
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
}
