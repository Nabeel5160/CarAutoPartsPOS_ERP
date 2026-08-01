using System.Collections.ObjectModel;
using CarAutoParts.Application.DTOs.Products;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarAutoParts.Presentation.ViewModels;

public partial class ProductsViewModel : ViewModelBase
{
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly IBrandService _brandService;
    private readonly ISnackbarService _snackbarService;
    private readonly IExceptionHandler _exceptionHandler;
    private readonly IDialogService _dialogService;
    private readonly INavigationState _navigationState;

    public ProductsViewModel(
        IProductService productService,
        ICategoryService categoryService,
        IBrandService brandService,
        ISnackbarService snackbarService,
        IExceptionHandler exceptionHandler,
        IDialogService dialogService,
        INavigationState navigationState)
    {
        _productService = productService;
        _categoryService = categoryService;
        _brandService = brandService;
        _snackbarService = snackbarService;
        _exceptionHandler = exceptionHandler;
        _dialogService = dialogService;
        _navigationState = navigationState;
    }

    public ObservableCollection<ProductListDto> Products { get; } = new();
    public ObservableCollection<CategoryDto> Categories { get; } = new();
    public ObservableCollection<BrandDto> Brands { get; } = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ProductListDto? _selectedProduct;

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string _editSku = string.Empty;

    [ObservableProperty]
    private string? _editBarcode;

    [ObservableProperty]
    private int _editCategoryId;

    [ObservableProperty]
    private int _editBrandId;

    [ObservableProperty]
    private decimal _editSalePrice;

    [ObservableProperty]
    private decimal _editPurchasePrice;

    [ObservableProperty]
    private int _editMinimumStock;

    [ObservableProperty]
    private bool _editIsActive = true;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private int _totalCount;

    private int? _editingId;

    public override async Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_navigationState.ProductSearch))
        {
            SearchText = _navigationState.ProductSearch;
            _navigationState.ProductSearch = null;
            CurrentPage = 1;
        }

        await LoadLookupsAsync();
        await LoadProductsAsync();
    }

    [RelayCommand]
    private async Task LoadProductsAsync()
    {
        await _exceptionHandler.ExecuteAsync(async () =>
        {
            IsBusy = true;
            var result = await _productService.GetProductsAsync(new ProductQueryDto
            {
                Search = SearchText,
                Page = CurrentPage,
                PageSize = 25
            });
            Products.Clear();
            foreach (var item in result.Items)
                Products.Add(item);
            TotalCount = result.TotalCount;
            TotalPages = Math.Max(1, result.TotalPages);
            IsBusy = false;
        }, "Products");
    }

    [RelayCommand]
    private void NewProduct()
    {
        _editingId = null;
        EditName = string.Empty;
        EditSku = string.Empty;
        EditBarcode = null;
        EditCategoryId = Categories.FirstOrDefault()?.Id ?? 0;
        EditBrandId = Brands.FirstOrDefault()?.Id ?? 0;
        EditSalePrice = 0;
        EditPurchasePrice = 0;
        EditMinimumStock = 0;
        EditIsActive = true;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void EditProduct(ProductListDto? product)
    {
        if (product is null)
            return;

        _editingId = product.Id;
        EditName = product.Name;
        EditSku = product.Sku;
        EditBarcode = product.Barcode;
        EditCategoryId = Categories.FirstOrDefault(c => c.Name == product.CategoryName)?.Id ?? EditCategoryId;
        EditBrandId = Brands.FirstOrDefault(b => b.Name == product.BrandName)?.Id ?? EditBrandId;
        EditSalePrice = product.SalePrice;
        EditPurchasePrice = product.CostPrice;
        EditMinimumStock = product.MinimumStock;
        EditIsActive = product.IsActive;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private async Task SaveProductAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName) || string.IsNullOrWhiteSpace(EditSku))
        {
            _snackbarService.Show("Name and SKU are required.");
            return;
        }

        var dto = new ProductCreateDto(
            EditName.Trim(),
            EditSku.Trim(),
            string.IsNullOrWhiteSpace(EditBarcode) ? null : EditBarcode.Trim(),
            null,
            null,
            EditCategoryId,
            EditBrandId,
            "PCS",
            EditPurchasePrice,
            EditSalePrice,
            EditMinimumStock,
            EditMinimumStock,
            null,
            null,
            null,
            18m,
            EditIsActive,
            false,
            false,
            null);

        await _exceptionHandler.ExecuteAsync(async () =>
        {
            IsBusy = true;
            if (_editingId is int id)
            {
                var result = await _productService.UpdateAsync(id, dto);
                if (!result.Succeeded)
                {
                    _snackbarService.Show(result.Error ?? "Update failed.");
                    return;
                }
                _snackbarService.Show("Product updated.");
            }
            else
            {
                var result = await _productService.CreateAsync(dto);
                if (!result.Succeeded)
                {
                    _snackbarService.Show(result.Error ?? "Create failed.");
                    return;
                }
                _snackbarService.Show("Product created.");
            }

            IsEditorOpen = false;
            await LoadProductsAsync();
            IsBusy = false;
        }, "Save product");
    }

    [RelayCommand]
    private void CancelEdit() => IsEditorOpen = false;

    [RelayCommand]
    private async Task DeleteProductAsync(ProductListDto? product)
    {
        if (product is null)
            return;

        var confirmed = await _dialogService.ConfirmAsync("Delete product", $"Delete {product.Name}?");
        if (!confirmed)
            return;

        await _exceptionHandler.ExecuteAsync(async () =>
        {
            var result = await _productService.DeleteAsync(product.Id);
            if (!result.Succeeded)
            {
                _snackbarService.Show(result.Error ?? "Delete failed.");
                return;
            }
            _snackbarService.Show("Product deleted.");
            await LoadProductsAsync();
        }, "Delete product");
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await LoadProductsAsync();
        }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await LoadProductsAsync();
        }
    }

    private async Task LoadLookupsAsync()
    {
        var categories = await _categoryService.GetTreeAsync();
        Categories.Clear();
        FlattenCategories(categories, Categories);

        var brands = await _brandService.GetAllAsync();
        Brands.Clear();
        foreach (var brand in brands)
            Brands.Add(brand);
    }

    private static void FlattenCategories(IReadOnlyList<CategoryDto> nodes, ICollection<CategoryDto> target)
    {
        foreach (var node in nodes)
        {
            target.Add(node);
            FlattenCategories(node.Children, target);
        }
    }
}
