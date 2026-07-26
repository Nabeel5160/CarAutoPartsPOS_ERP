using System.IO;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Media.Imaging;

namespace CarAutoParts.Presentation.ViewModels;

public partial class BarcodeViewModel : ViewModelBase
{
    private readonly IBarcodeService _barcodeService;
    private readonly IExceptionHandler _exceptionHandler;

    public BarcodeViewModel(IBarcodeService barcodeService, IExceptionHandler exceptionHandler)
    {
        _barcodeService = barcodeService;
        _exceptionHandler = exceptionHandler;
    }

    [ObservableProperty]
    private string _barcodeText = "CAP-0001";

    [ObservableProperty]
    private BitmapImage? _barcodeImage;

    public override async Task InitializeAsync()
    {
        await GenerateAsync();
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        await _exceptionHandler.ExecuteAsync(async () =>
        {
            IsBusy = true;
            var bytes = await Task.Run(() => _barcodeService.GenerateBarcodeImage(BarcodeText));
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                BarcodeImage = LoadBitmap(bytes);
                StatusMessage = $"Barcode generated for {BarcodeText}";
            });
            IsBusy = false;
        }, "Barcode");
    }

    private static BitmapImage LoadBitmap(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
