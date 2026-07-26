using QRCoder;

namespace CarAutoParts.Infrastructure.Printing;

public static class QrCodeHelper
{
    /// <summary>Generates a QR code as PNG bytes for the given text.</summary>
    public static byte[] GeneratePng(string text, int pixelsPerModule = 6)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        var pngQr = new PngByteQRCode(data);
        return pngQr.GetGraphic(pixelsPerModule);
    }
}
