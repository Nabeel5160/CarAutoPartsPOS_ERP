using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace PosWpf.Services;

public static class QrCodeHelper
{
    /// <summary>Generates a QR code bitmap (as a WPF image source) for the given text.</summary>
    public static BitmapImage Generate(string text, int pixelsPerModule = 6)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        var pngQr = new PngByteQRCode(data);
        byte[] bytes = pngQr.GetGraphic(pixelsPerModule);

        var image = new BitmapImage();
        using var ms = new MemoryStream(bytes);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = ms;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
