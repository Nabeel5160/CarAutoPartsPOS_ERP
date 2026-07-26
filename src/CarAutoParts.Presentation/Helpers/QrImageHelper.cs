using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace CarAutoParts.Presentation.Helpers;

public static class QrImageHelper
{
    public static byte[] GeneratePng(string text, int pixelsPerModule = 4)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        var pngQr = new PngByteQRCode(data);
        return pngQr.GetGraphic(pixelsPerModule);
    }

    public static BitmapImage FromPngBytes(byte[] bytes)
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
