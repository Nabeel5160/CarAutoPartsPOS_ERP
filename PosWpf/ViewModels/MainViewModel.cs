using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using PosWpf.Common;
using PosWpf.Models;
using PosWpf.Services;

namespace PosWpf.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly IFbrService _fbrService;
    private readonly SellerSettings _seller;
    private readonly bool _fbrLive;
    private readonly bool _useSandbox;

    public MainViewModel(IFbrService fbrService, SellerSettings seller, bool fbrLive, bool useSandbox)
    {
        _fbrService = fbrService;
        _seller = seller;
        _fbrLive = fbrLive;
        _useSandbox = useSandbox;

        foreach (var p in ProductCatalog.GetProducts())
            Products.Add(p);

        foreach (var p in FbrLookups.Provinces) Provinces.Add(p);
        foreach (var r in FbrLookups.RegistrationTypes) RegistrationTypes.Add(r);
        foreach (var s in FbrLookups.SandboxScenarios) SandboxScenarios.Add(s);
        foreach (var s in FbrLookups.SaleTypes) SaleTypes.Add(s);

        BuyerProvince = seller.Province;
        SaleType = FbrLookups.SaleTypes[0];

        Cart.CollectionChanged += (_, _) => RecalculateTotals();

        AddToCartCommand = new RelayCommand(p => AddToCart(p as Product), p => p is Product);
        IncreaseQtyCommand = new RelayCommand(p => ChangeQty(p as CartItem, +1), p => p is CartItem);
        DecreaseQtyCommand = new RelayCommand(p => ChangeQty(p as CartItem, -1), p => p is CartItem);
        RemoveItemCommand = new RelayCommand(p => RemoveItem(p as CartItem), p => p is CartItem);
        ClearCartCommand = new RelayCommand(() => ClearCart(), () => Cart.Count > 0);
        CheckoutCommand = new AsyncRelayCommand(CheckoutAsync, () => Cart.Count > 0);
        PrintReceiptCommand = new RelayCommand(PrintReceipt, () => LastReceipt is not null);
    }

    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<CartItem> Cart { get; } = new();
    public ObservableCollection<string> Provinces { get; } = new();
    public ObservableCollection<string> RegistrationTypes { get; } = new();
    public ObservableCollection<string> SandboxScenarios { get; } = new();
    public ObservableCollection<string> SaleTypes { get; } = new();

    public RelayCommand AddToCartCommand { get; }
    public RelayCommand IncreaseQtyCommand { get; }
    public RelayCommand DecreaseQtyCommand { get; }
    public RelayCommand RemoveItemCommand { get; }
    public RelayCommand ClearCartCommand { get; }
    public AsyncRelayCommand CheckoutCommand { get; }
    public RelayCommand PrintReceiptCommand { get; }

    // ---- Buyer ----
    private string _buyerName = "Walk-in Customer";
    public string BuyerName { get => _buyerName; set => SetProperty(ref _buyerName, value); }

    private string _buyerNtn = string.Empty;
    public string BuyerNtn { get => _buyerNtn; set => SetProperty(ref _buyerNtn, value); }

    private string _buyerRegistrationType = "Unregistered";
    public string BuyerRegistrationType
    {
        get => _buyerRegistrationType;
        set => SetProperty(ref _buyerRegistrationType, value);
    }

    private string _buyerProvince = string.Empty;
    public string BuyerProvince { get => _buyerProvince; set => SetProperty(ref _buyerProvince, value); }

    private string _buyerAddress = string.Empty;
    public string BuyerAddress { get => _buyerAddress; set => SetProperty(ref _buyerAddress, value); }

    // ---- FBR tax / scenario ----
    private string _scenarioId = string.Empty;
    public string ScenarioId { get => _scenarioId; set => SetProperty(ref _scenarioId, value); }

    private string _sroScheduleNo = string.Empty;
    public string SroScheduleNo { get => _sroScheduleNo; set => SetProperty(ref _sroScheduleNo, value); }

    private string _sroItemSerialNo = string.Empty;
    public string SroItemSerialNo { get => _sroItemSerialNo; set => SetProperty(ref _sroItemSerialNo, value); }

    private string _saleType = string.Empty;
    public string SaleType { get => _saleType; set => SetProperty(ref _saleType, value); }

    private bool _isFbrDetailsExpanded;
    public bool IsFbrDetailsExpanded
    {
        get => _isFbrDetailsExpanded;
        set => SetProperty(ref _isFbrDetailsExpanded, value);
    }

    public bool ShowScenarioField => _useSandbox;

    // ---- Search ----
    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
    }

    // ---- Totals ----
    private decimal _subtotal;
    public decimal Subtotal { get => _subtotal; private set => SetProperty(ref _subtotal, value); }

    private decimal _taxTotal;
    public decimal TaxTotal { get => _taxTotal; private set => SetProperty(ref _taxTotal, value); }

    private decimal _grandTotal;
    public decimal GrandTotal { get => _grandTotal; private set => SetProperty(ref _grandTotal, value); }

    private int _itemCount;
    public int ItemCount { get => _itemCount; private set => SetProperty(ref _itemCount, value); }

    // ---- Status / FBR result ----
    private string _statusMessage = "Ready.";
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

    private string _fbrModeLabel = string.Empty;
    public string FbrModeLabel { get => _fbrModeLabel; private set => SetProperty(ref _fbrModeLabel, value); }

    private string? _lastFbrInvoiceNumber;
    public string? LastFbrInvoiceNumber { get => _lastFbrInvoiceNumber; private set => SetProperty(ref _lastFbrInvoiceNumber, value); }

    private BitmapImage? _qrImage;
    public BitmapImage? QrImage { get => _qrImage; private set => SetProperty(ref _qrImage, value); }

    private bool _lastPostSuccess;
    public bool LastPostSuccess { get => _lastPostSuccess; private set => SetProperty(ref _lastPostSuccess, value); }

    private ReceiptData? _lastReceipt;
    public ReceiptData? LastReceipt
    {
        get => _lastReceipt;
        private set
        {
            if (SetProperty(ref _lastReceipt, value))
                PrintReceiptCommand.RaiseCanExecuteChanged();
        }
    }

    public void Initialize()
    {
        FbrModeLabel = _fbrLive
            ? (_useSandbox ? "FBR: SANDBOX (live token)" : "FBR: PRODUCTION (live token)")
            : "FBR: OFFLINE / STUB (no token)";
        RecalculateTotals();
    }

    private FbrBuyerDetails BuildBuyerDetails() => new()
    {
        BuyerName = BuyerName,
        BuyerNtn = string.IsNullOrWhiteSpace(BuyerNtn) ? null : BuyerNtn.Trim(),
        BuyerRegistrationType = BuyerRegistrationType,
        BuyerProvince = BuyerProvince,
        BuyerAddress = BuyerAddress,
        ScenarioId = string.IsNullOrWhiteSpace(ScenarioId) ? null : ScenarioId.Trim(),
        SroScheduleNo = SroScheduleNo.Trim(),
        SroItemSerialNo = SroItemSerialNo.Trim(),
        SaleType = SaleType
    };

    private void ApplyFilter()
    {
        Products.Clear();
        var all = ProductCatalog.GetProducts();
        var q = SearchText?.Trim();
        var filtered = string.IsNullOrEmpty(q)
            ? all
            : all.Where(p =>
                p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Sku.Contains(q, StringComparison.OrdinalIgnoreCase));
        foreach (var p in filtered) Products.Add(p);
    }

    private void AddToCart(Product? product)
    {
        if (product is null) return;
        var existing = Cart.FirstOrDefault(c => c.Product.Sku == product.Sku);
        if (existing is not null)
        {
            existing.Quantity++;
            existing.PropertyChanged -= OnCartItemChanged;
            existing.PropertyChanged += OnCartItemChanged;
        }
        else
        {
            var item = new CartItem { Product = product, Quantity = 1 };
            item.PropertyChanged += OnCartItemChanged;
            Cart.Add(item);
        }
        RecalculateTotals();
    }

    private void OnCartItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => RecalculateTotals();

    private void ChangeQty(CartItem? item, int delta)
    {
        if (item is null) return;
        var newQty = item.Quantity + delta;
        if (newQty <= 0) { RemoveItem(item); return; }
        item.Quantity = newQty;
        RecalculateTotals();
    }

    private void RemoveItem(CartItem? item)
    {
        if (item is null) return;
        item.PropertyChanged -= OnCartItemChanged;
        Cart.Remove(item);
        RecalculateTotals();
    }

    private void ClearCart()
    {
        foreach (var item in Cart) item.PropertyChanged -= OnCartItemChanged;
        Cart.Clear();
        QrImage = null;
        LastFbrInvoiceNumber = null;
        LastReceipt = null;
        StatusMessage = "Cart cleared.";
        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        Subtotal = Cart.Sum(c => c.LineSubtotal);
        TaxTotal = Cart.Sum(c => c.LineTax);
        GrandTotal = Subtotal + TaxTotal;
        ItemCount = Cart.Sum(c => c.Quantity);
    }

    private string? ValidateCheckout()
    {
        if (string.Equals(BuyerRegistrationType, "Registered", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(BuyerNtn))
            return "Buyer NTN/CNIC is required for Registered buyers.";

        if (string.IsNullOrWhiteSpace(BuyerProvince))
            return "Buyer province is required.";

        return null;
    }

    private async Task CheckoutAsync()
    {
        if (Cart.Count == 0) return;

        var validationError = ValidateCheckout();
        if (validationError is not null)
        {
            StatusMessage = validationError;
            LastPostSuccess = false;
            return;
        }

        IsBusy = true;
        LastPostSuccess = false;
        QrImage = null;
        LastReceipt = null;
        StatusMessage = "Posting invoice to FBR...";

        try
        {
            var posRef = $"POS-{_seller.PosId}-{DateTime.Now:yyyyMMddHHmmss}";
            var buyer = BuildBuyerDetails();
            var request = InvoiceBuilder.Build(Cart, _seller, buyer, posRef);

            var result = await _fbrService.PostInvoiceAsync(request);

            if (result.Success)
            {
                LastPostSuccess = true;
                LastFbrInvoiceNumber = result.InvoiceNumber;
                StatusMessage = $"{result.Message}  FBR Invoice #: {result.InvoiceNumber}";

                var qrPayload = BuildQrPayload(result.InvoiceNumber!, posRef);
                QrImage = QrCodeHelper.Generate(qrPayload);

                LastReceipt = new ReceiptData
                {
                    Seller = _seller,
                    BuyerName = buyer.BuyerName,
                    BuyerNtn = buyer.BuyerNtn,
                    BuyerRegistrationType = buyer.BuyerRegistrationType,
                    PosRef = posRef,
                    FbrInvoiceNumber = result.InvoiceNumber!,
                    QrPayload = qrPayload,
                    SaleDate = DateTime.Now,
                    Lines = Cart.Select(c => new ReceiptLine
                    {
                        Name = c.Product.Name,
                        Quantity = c.Quantity,
                        UnitPrice = c.Product.UnitPrice,
                        LineTax = c.LineTax,
                        LineTotal = c.LineTotal
                    }).ToList(),
                    Subtotal = Subtotal,
                    TaxTotal = TaxTotal,
                    GrandTotal = GrandTotal,
                    ScenarioId = buyer.ScenarioId,
                    SroScheduleNo = string.IsNullOrWhiteSpace(buyer.SroScheduleNo) ? null : buyer.SroScheduleNo,
                    SroItemSerialNo = string.IsNullOrWhiteSpace(buyer.SroItemSerialNo) ? null : buyer.SroItemSerialNo,
                    WasStubbed = result.WasStubbed
                };

                ReceiptPrintService.Print(LastReceipt);
            }
            else
            {
                LastPostSuccess = false;
                StatusMessage = $"FAILED: {result.Message}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unexpected error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PrintReceipt()
    {
        if (LastReceipt is null) return;
        if (ReceiptPrintService.Print(LastReceipt))
            StatusMessage = "Receipt sent to printer.";
    }

    private string BuildQrPayload(string fbrInvoiceNumber, string posRef)
        => $"FBR-IRN:{fbrInvoiceNumber}|REF:{posRef}|NTN:{_seller.NTNCNIC}|TOTAL:{GrandTotal:0.00}|DATE:{DateTime.Now:yyyy-MM-dd}";
}
