using System.Collections.ObjectModel;

using System.IO;

using CarAutoParts.Application.Common;

using CarAutoParts.Application.DTOs.Fbr;

using CarAutoParts.Application.DTOs.Pos;

using CarAutoParts.Application.DTOs.Sales;

using CarAutoParts.Application.Interfaces;

using CarAutoParts.Domain.Enums;

using CarAutoParts.Infrastructure.Printing;

using CarAutoParts.Presentation.Services;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Configuration;

using Microsoft.Win32;



namespace CarAutoParts.Presentation.ViewModels;



public partial class SalesInvoicesViewModel : EntityModuleViewModelBase<SalesInvoiceListDto>

{

    private readonly ISalesService _service;

    private readonly ISettingsService _settingsService;

    private readonly ReceiptPrintService _printService;

    private readonly IConfiguration _configuration;
    private readonly INavigationState _navigationState;
    private SalesInvoiceDetailDto? _detail;

    public SalesInvoicesViewModel(
        ISalesService service,
        ISettingsService settingsService,
        ReceiptPrintService printService,
        IConfiguration configuration,
        INavigationState navigationState,
        IExceptionHandler exceptionHandler,
        ISnackbarService snackbarService,
        IDialogService dialogService)
        : base("Sales Invoices", "View invoices, line items, and FBR references", exceptionHandler, snackbarService, dialogService)
    {
        _service = service;
        _settingsService = settingsService;
        _printService = printService;
        _configuration = configuration;
        _navigationState = navigationState;
        ShowPaging = true;
        ShowPrimaryAction = false;
    }

    public override async Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_navigationState.SalesInvoiceSearch))
        {
            SearchText = _navigationState.SalesInvoiceSearch;
            _navigationState.SalesInvoiceSearch = null;
        }

        await base.InitializeAsync();
    }

    public ObservableCollection<SalesInvoiceLineDto> DetailLines { get; } = new();



    [ObservableProperty]

    private SalesInvoiceListDto? _selectedItem;



    [ObservableProperty]

    private string _editorTitle = "Invoice";



    [ObservableProperty]

    private string? _detailInvoiceNumber;



    [ObservableProperty]

    private string? _detailCustomerName;



    [ObservableProperty]

    private DateTime? _detailInvoiceDate;



    [ObservableProperty]

    private decimal _detailSubTotal;



    [ObservableProperty]

    private decimal _detailTaxAmount;



    [ObservableProperty]

    private decimal _detailDiscountAmount;



    [ObservableProperty]

    private decimal _detailGrandTotal;



    [ObservableProperty]

    private PaymentStatus _detailPaymentStatus;



    [ObservableProperty]

    private string? _detailPosReference;



    [ObservableProperty]

    private string? _detailBuyerName;



    [ObservableProperty]

    private string? _detailBuyerNtn;



    [ObservableProperty]

    private string? _detailBuyerProvince;



    [ObservableProperty]

    private string? _detailFbrInvoiceNumber;



    [ObservableProperty]

    private FbrSubmissionStatus? _detailFbrStatus;



    public bool CanPrint => _detail is not null;



    protected override async Task LoadItemsAsync()

    {

        var result = await _service.GetInvoicesAsync(new QuerySpec

        {

            Search = SearchText,

            Page = CurrentPage,

            PageSize = 25

        });



        foreach (var item in result.Items)

            Items.Add(item);



        TotalPages = Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)result.PageSize));

    }



    private void LoadDetail(SalesInvoiceDetailDto detail)

    {

        _detail = detail;

        EditorTitle = $"Invoice {detail.InvoiceNumber}";

        DetailInvoiceNumber = detail.InvoiceNumber;

        DetailCustomerName = detail.CustomerName ?? detail.BuyerName;

        DetailInvoiceDate = detail.InvoiceDate;

        DetailSubTotal = detail.SubTotal;

        DetailTaxAmount = detail.TaxAmount;

        DetailDiscountAmount = detail.DiscountAmount;

        DetailGrandTotal = detail.GrandTotal;

        DetailPaymentStatus = detail.PaymentStatus;

        DetailPosReference = detail.PosReference;

        DetailBuyerName = detail.BuyerName;

        DetailBuyerNtn = detail.BuyerNtnCnic;

        DetailBuyerProvince = detail.BuyerProvince;

        DetailFbrInvoiceNumber = detail.FbrInvoiceNumber;

        DetailFbrStatus = detail.FbrStatus;

        DetailLines.Clear();

        foreach (var line in detail.Lines)

            DetailLines.Add(line);

        OnPropertyChanged(nameof(CanPrint));

    }



    [RelayCommand]

    private async Task OpenAsync(SalesInvoiceListDto? item)

    {

        if (item is null)

            return;



        await ExceptionHandler.ExecuteAsync(async () =>

        {

            var detail = await _service.GetInvoiceByIdAsync(item.Id);

            if (detail is null)

            {

                SnackbarService.Show("Invoice not found.");

                return;

            }



            LoadDetail(detail);

            IsEditorOpen = true;

        }, "Open invoice");

    }



    [RelayCommand]

    private void CloseDetail() => IsEditorOpen = false;



    [RelayCommand]

    private async Task PrintAsync()

    {

        if (_detail is null)

            return;



        var dialog = new SaveFileDialog

        {

            Title = "Save receipt PDF",

            Filter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*",

            FileName = $"{_detail.InvoiceNumber}.pdf",

            DefaultExt = ".pdf"

        };



        if (dialog.ShowDialog() != true)

            return;



        await ExceptionHandler.ExecuteAsync(async () =>

        {

            var settings = await _settingsService.GetSettingsAsync();

            var fbrNumber = _detail.FbrInvoiceNumber ?? _detail.InvoiceNumber;

            var receipt = new ReceiptDataDto

            {

                Seller = new FbrSellerSettingsDto

                {

                    BusinessName = settings.CompanyName,

                    NTNCNIC = settings.Ntn ?? string.Empty,

                    Address = settings.Address ?? string.Empty,

                    Province = _configuration["Seller:Province"] ?? settings.City ?? "Sindh",

                    PosId = settings.PosId ?? "POS-001"

                },

                BuyerName = _detail.BuyerName ?? _detail.CustomerName ?? "Walk-in Customer",

                BuyerNtn = _detail.BuyerNtnCnic,

                BuyerRegistrationType = string.IsNullOrWhiteSpace(_detail.BuyerNtnCnic) ? "Unregistered" : "Registered",

                PosRef = _detail.PosReference ?? _detail.InvoiceNumber,

                FbrInvoiceNumber = fbrNumber,

                QrPayload = $"FBR-IRN:{fbrNumber}|TOTAL:{_detail.GrandTotal:0.00}",

                SaleDate = _detail.InvoiceDate,

                Lines = _detail.Lines.Select(l => new ReceiptLineDto

                {

                    Name = l.ProductName,

                    Quantity = l.Quantity,

                    UnitPrice = l.UnitPrice,

                    LineTax = l.TaxAmount,

                    LineTotal = l.LineTotal

                }).ToList(),

                Subtotal = _detail.SubTotal,

                TaxTotal = _detail.TaxAmount,

                GrandTotal = _detail.GrandTotal,

                WasStubbed = _detail.FbrStatus == FbrSubmissionStatus.Stub

            };



            await _printService.SavePdfAsync(receipt, dialog.FileName);

            SnackbarService.Show($"Receipt saved to {Path.GetFileName(dialog.FileName)}");

        }, "Print invoice");

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

