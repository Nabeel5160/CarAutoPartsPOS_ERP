using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using CarAutoParts.Application.DTOs.Pos;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Infrastructure.Fbr;
using CarAutoParts.Presentation.Helpers;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace CarAutoParts.Presentation.ViewModels;

public partial class PosViewModel : ViewModelBase
{
    private readonly IPosCheckoutService _posService;
    private readonly IWarehouseService _warehouseService;
    private readonly IExceptionHandler _exceptionHandler;
    private readonly ISnackbarService _snackbarService;
    private readonly FbrOptions _fbrOptions;
    private readonly IConfiguration _configuration;
    private int _warehouseId;

    public PosViewModel(
        IPosCheckoutService posService,
        IWarehouseService warehouseService,
        IExceptionHandler exceptionHandler,
        ISnackbarService snackbarService,
        IOptions<FbrOptions> fbrOptions,
        IConfiguration configuration)
    {
        _posService = posService;
        _warehouseService = warehouseService;
        _exceptionHandler = exceptionHandler;
        _snackbarService = snackbarService;
        _fbrOptions = fbrOptions.Value;
        _configuration = configuration;

        Cart.CollectionChanged += (_, _) =>
        {
            RecalculateTotals();
            RefreshCheckoutState();
        };

        Provinces.Clear();
        foreach (var p in new[] { "Punjab", "Sindh", "KPK", "Balochistan", "ICT", "AJK", "GB" })
            Provinces.Add(p);
        RegistrationTypes.Clear();
        foreach (var r in new[] { "Unregistered", "Registered" })
            RegistrationTypes.Add(r);
        SaleTypes.Clear();
        foreach (var s in new[] { "Local", "Export", "Inter-Provincial" })
            SaleTypes.Add(s);
        SandboxScenarios.Clear();
        foreach (var sn in new[] { "SN001", "SN002", "SN003", "SN004", "SN005" })
            SandboxScenarios.Add(sn);
        BuyerProvince = _configuration["Seller:Province"] ?? "Sindh";
        SaleType = SaleTypes[0];
        StatusMessage = "Ready.";
    }

    public ObservableCollection<PosProductDto> Products { get; } = new();
    public ObservableCollection<PosCartLineViewModel> Cart { get; } = new();
    public ObservableCollection<string> Provinces { get; } = new();
    public ObservableCollection<string> RegistrationTypes { get; } = new();
    public ObservableCollection<string> SandboxScenarios { get; } = new();
    public ObservableCollection<string> SaleTypes { get; } = new();

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _barcodeInput = string.Empty;
    [ObservableProperty] private string _buyerName = "Walk-in Customer";
    [ObservableProperty] private string _buyerNtn = string.Empty;
    [ObservableProperty] private string _buyerRegistrationType = "Unregistered";
    [ObservableProperty] private string _buyerProvince = string.Empty;
    [ObservableProperty] private string _buyerAddress = string.Empty;
    [ObservableProperty] private string _scenarioId = string.Empty;
    [ObservableProperty] private string _sroScheduleNo = string.Empty;
    [ObservableProperty] private string _sroItemSerialNo = string.Empty;
    [ObservableProperty] private string _saleType = string.Empty;
    [ObservableProperty] private bool _isFbrDetailsExpanded;
    [ObservableProperty] private decimal _subtotal;
    [ObservableProperty] private decimal _taxTotal;
    [ObservableProperty] private decimal _grandTotal;
    [ObservableProperty] private int _itemCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusBarBackground))]
    private string? _lastFbrInvoiceNumber;
    [ObservableProperty] private BitmapImage? _qrImage;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusBarBackground))]
    private bool _lastPostSuccess;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusBarBackground))]
    private bool _lastCheckoutFbrFailed;
    [ObservableProperty] private string _fbrModeLabel = string.Empty;

    public bool CanCheckout => Cart.Count > 0 && !IsBusy;
    public string StatusBarBackground => LastCheckoutFbrFailed ? "#B91C1C" : LastPostSuccess ? "#15803D" : "#0F172A";

    private void RefreshCheckoutState()
    {
        OnPropertyChanged(nameof(CanCheckout));
        CheckoutCommand.NotifyCanExecuteChanged();
    }

    public bool ShowScenarioField => _fbrOptions.UseSandbox;

    public override async Task InitializeAsync()
    {
        FbrModeLabel = _fbrOptions.HasToken
            ? (_fbrOptions.UseSandbox ? "FBR: SANDBOX" : "FBR: PRODUCTION")
            : "FBR: OFFLINE / STUB";

        var warehouses = await _warehouseService.GetAllAsync();
        _warehouseId = warehouses.FirstOrDefault(w => w.IsDefault)?.Id ?? warehouses.FirstOrDefault()?.Id ?? 1;
        await LoadProductsAsync();
    }

    [RelayCommand]
    private async Task LoadProductsAsync()
    {
        await _exceptionHandler.ExecuteAsync(async () =>
        {
            var items = await _posService.GetPosProductsAsync(SearchText);
            Products.Clear();
            foreach (var item in items)
                Products.Add(item);
        }, "POS products");
    }

    partial void OnSearchTextChanged(string value) => _ = LoadProductsAsync();

    [RelayCommand]
    private void AddToCart(PosProductDto? product)
    {
        if (product is null)
            return;

        var existing = Cart.FirstOrDefault(c => c.Product.Id == product.Id);
        if (existing is not null)
            existing.Quantity++;
        else
            Cart.Add(new PosCartLineViewModel(product, 1));

        RecalculateTotals();
    }

    [RelayCommand]
    private void IncreaseQty(PosCartLineViewModel? line)
    {
        if (line is null) return;
        line.Quantity++;
        RecalculateTotals();
    }

    [RelayCommand]
    private void DecreaseQty(PosCartLineViewModel? line)
    {
        if (line is null) return;
        if (line.Quantity <= 1) Cart.Remove(line);
        else line.Quantity--;
        RecalculateTotals();
    }

    [RelayCommand]
    private void RemoveItem(PosCartLineViewModel? line)
    {
        if (line is null) return;
        Cart.Remove(line);
        RecalculateTotals();
    }

    [RelayCommand]
    private void ClearCart()
    {
        Cart.Clear();
        QrImage = null;
        LastFbrInvoiceNumber = null;
        StatusMessage = "Cart cleared.";
        RecalculateTotals();
    }

    [RelayCommand]
    private async Task ProcessBarcodeScanAsync()
    {
        var code = BarcodeInput?.Trim();
        if (string.IsNullOrWhiteSpace(code))
            return;

        PosProductDto? product = Products.FirstOrDefault(p =>
            string.Equals(p.Barcode, code, StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.Sku, code, StringComparison.OrdinalIgnoreCase));

        if (product is null)
        {
            var items = await _posService.GetPosProductsAsync(code);
            product = items.FirstOrDefault(p =>
                string.Equals(p.Barcode, code, StringComparison.OrdinalIgnoreCase))
                ?? items.FirstOrDefault();
        }

        if (product is null)
        {
            StatusMessage = $"No product found for barcode: {code}";
            LastCheckoutFbrFailed = false;
            return;
        }

        AddToCart(product);
        BarcodeInput = string.Empty;
        StatusMessage = $"Added {product.Name} to cart.";
        LastCheckoutFbrFailed = false;
    }

    [RelayCommand(CanExecute = nameof(CanCheckout))]
    private async Task CheckoutAsync()
    {
        if (Cart.Count == 0)
            return;

        if (string.Equals(BuyerRegistrationType, "Registered", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(BuyerNtn))
        {
            StatusMessage = "Buyer NTN/CNIC is required for Registered buyers.";
            return;
        }

        IsBusy = true;
        RefreshCheckoutState();
        LastPostSuccess = false;
        LastCheckoutFbrFailed = false;
        QrImage = null;
        StatusMessage = "Processing checkout...";

        await _exceptionHandler.ExecuteAsync(async () =>
        {
            var dto = new PosCheckoutDto(
                _warehouseId,
                null,
                new PosBuyerDto(
                    BuyerName,
                    string.IsNullOrWhiteSpace(BuyerNtn) ? null : BuyerNtn.Trim(),
                    BuyerRegistrationType,
                    BuyerProvince,
                    BuyerAddress,
                    string.IsNullOrWhiteSpace(SroScheduleNo) ? null : SroScheduleNo,
                    string.IsNullOrWhiteSpace(SroItemSerialNo) ? null : SroItemSerialNo),
                "Cash",
                0,
                null,
                string.IsNullOrWhiteSpace(ScenarioId) ? null : ScenarioId.Trim(),
                SaleType,
                Cart.Select(c => new PosCheckoutLineDto(c.Product.Id, c.Quantity, null, 0)).ToList());

            var result = await _posService.CheckoutAsync(dto);
            LastPostSuccess = result.FbrSuccess;
            LastCheckoutFbrFailed = !result.FbrSuccess;
            LastFbrInvoiceNumber = result.FbrInvoiceNumber ?? result.InvoiceNumber;

            if (result.FbrSuccess)
                StatusMessage = result.FbrMessage ?? $"Invoice {result.InvoiceNumber} posted to FBR.";
            else
                StatusMessage = $"FBR error: {result.FbrMessage ?? "Submission failed"}. Invoice {result.InvoiceNumber} saved locally.";

            if (!string.IsNullOrWhiteSpace(LastFbrInvoiceNumber) && result.FbrSuccess)
            {
                var payload = $"FBR-IRN:{LastFbrInvoiceNumber}|TOTAL:{result.GrandTotal:0.00}";
                QrImage = QrImageHelper.FromPngBytes(QrImageHelper.GeneratePng(payload));
            }

            _snackbarService.Show(result.FbrSuccess ? "Checkout completed." : "Checkout saved — FBR submission failed.");
            Cart.Clear();
            RecalculateTotals();
            await LoadProductsAsync();
            IsBusy = false;
            RefreshCheckoutState();
        }, "POS checkout");
    }

    private void RecalculateTotals()
    {
        Subtotal = Cart.Sum(c => c.LineSubtotal);
        TaxTotal = Cart.Sum(c => c.LineTax);
        GrandTotal = Subtotal + TaxTotal;
        ItemCount = (int)Cart.Sum(c => c.Quantity);
    }
}

public partial class PosCartLineViewModel : ObservableObject
{
    public PosCartLineViewModel(PosProductDto product, decimal quantity)
    {
        Product = product;
        _quantity = quantity;
    }

    public PosProductDto Product { get; }

    [ObservableProperty]
    private decimal _quantity;

    public decimal LineSubtotal => Product.SalePrice * Quantity;
    public decimal LineTax => LineSubtotal * Product.TaxRatePercent / 100m;
    public decimal LineTotal => LineSubtotal + LineTax;

    partial void OnQuantityChanged(decimal value)
    {
        OnPropertyChanged(nameof(LineSubtotal));
        OnPropertyChanged(nameof(LineTax));
        OnPropertyChanged(nameof(LineTotal));
    }
}
