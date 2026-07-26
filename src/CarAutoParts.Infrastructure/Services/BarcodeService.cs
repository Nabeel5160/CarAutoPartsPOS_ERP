using CarAutoParts.Application.Interfaces;

using Microsoft.Extensions.Logging;

using ZXing;

using ZXing.Common;



namespace CarAutoParts.Infrastructure.Services;



public class BarcodeService : IBarcodeService

{

    private readonly ILogger<BarcodeService> _logger;



    public BarcodeService(ILogger<BarcodeService> logger)

    {

        _logger = logger;

    }



    public byte[] GenerateBarcodeImage(string code, int width = 300, int height = 100)

    {

        var writer = new BarcodeWriterPixelData

        {

            Format = BarcodeFormat.CODE_128,

            Options = new EncodingOptions

            {

                Width = width,

                Height = height,

                Margin = 2,

                PureBarcode = false

            }

        };



        var pixelData = writer.Write(code);

        return EncodeBmp(pixelData.Pixels, pixelData.Width, pixelData.Height);

    }



    public Task PrintBarcodeAsync(string code, string label, CancellationToken ct = default)

    {

        _logger.LogInformation("Barcode print requested for {Code} ({Label}). Printing is handled by the presentation layer.", code, label);

        return Task.CompletedTask;

    }



    private static byte[] EncodeBmp(byte[] pixels, int width, int height)

    {

        const int headerSize = 54;

        const int paletteSize = 256 * 4;

        var rowSize = ((width + 3) / 4) * 4;

        var imageSize = rowSize * height;

        var fileSize = headerSize + paletteSize + imageSize;

        var buffer = new byte[fileSize];



        buffer[0] = (byte)'B';

        buffer[1] = (byte)'M';

        WriteInt32(buffer, 2, fileSize);

        WriteInt32(buffer, 10, headerSize + paletteSize);

        WriteInt32(buffer, 14, 40);

        WriteInt32(buffer, 18, width);

        WriteInt32(buffer, 22, height);

        buffer[26] = 1;

        buffer[28] = 8;

        WriteInt32(buffer, 34, imageSize);



        for (var i = 0; i < 256; i++)

        {

            var offset = headerSize + i * 4;

            buffer[offset] = (byte)i;

            buffer[offset + 1] = (byte)i;

            buffer[offset + 2] = (byte)i;

        }



        var dataOffset = headerSize + paletteSize;

        for (var y = 0; y < height; y++)

        {

            var srcRow = (height - 1 - y) * width;

            var destRow = dataOffset + y * rowSize;

            for (var x = 0; x < width; x++)

                buffer[destRow + x] = pixels[srcRow + x];

        }



        return buffer;

    }



    private static void WriteInt32(byte[] buffer, int offset, int value)

    {

        buffer[offset] = (byte)value;

        buffer[offset + 1] = (byte)(value >> 8);

        buffer[offset + 2] = (byte)(value >> 16);

        buffer[offset + 3] = (byte)(value >> 24);

    }

}


