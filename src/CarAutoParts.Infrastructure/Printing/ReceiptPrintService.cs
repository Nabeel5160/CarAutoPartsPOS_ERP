using CarAutoParts.Application.DTOs.Pos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CarAutoParts.Infrastructure.Printing;

/// <summary>Generates thermal-style receipt PDFs with line items and FBR QR code.</summary>
public class ReceiptPrintService
{
    static ReceiptPrintService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GeneratePdf(ReceiptDataDto receipt)
    {
        var qrBytes = QrCodeHelper.GeneratePng(receipt.QrPayload, pixelsPerModule: 4);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(80, 200, Unit.Millimetre);
                page.Margin(5);
                page.DefaultTextStyle(x => x.FontSize(8).FontFamily(Fonts.CourierNew));

                page.Content().Column(col =>
                {
                    col.Spacing(2);
                    col.Item().AlignCenter().Text(receipt.Seller.BusinessName).Bold().FontSize(10);
                    col.Item().AlignCenter().Text($"NTN/CNIC: {receipt.Seller.NTNCNIC}");
                    col.Item().AlignCenter().Text(receipt.Seller.Address ?? string.Empty);
                    col.Item().AlignCenter().Text($"{receipt.Seller.Province}  •  POS {receipt.Seller.PosId}");
                    col.Item().PaddingVertical(4).LineHorizontal(1);
                    col.Item().AlignCenter().Text("SALES RECEIPT").Bold();
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Date:");
                        r.RelativeItem().AlignRight().Text(receipt.SaleDate.ToString("dd-MMM-yyyy HH:mm"));
                    });
                    col.Item().Row(r => { r.RelativeItem().Text("POS Ref:"); r.RelativeItem().AlignRight().Text(receipt.PosRef); });
                    col.Item().Row(r => { r.RelativeItem().Text("Buyer:"); r.RelativeItem().AlignRight().Text(receipt.BuyerName); });
                    col.Item().Row(r => { r.RelativeItem().Text("Reg. Type:"); r.RelativeItem().AlignRight().Text(receipt.BuyerRegistrationType); });

                    if (!string.IsNullOrWhiteSpace(receipt.BuyerNtn))
                        col.Item().Row(r => { r.RelativeItem().Text("Buyer NTN:"); r.RelativeItem().AlignRight().Text(receipt.BuyerNtn); });

                    col.Item().PaddingVertical(4).LineHorizontal(1);

                    foreach (var line in receipt.Lines)
                    {
                        col.Item().Text(Truncate(line.Name, 36)).SemiBold();
                        col.Item().Text($"  {line.Quantity} x Rs {line.UnitPrice:N2}   Tax Rs {line.LineTax:N2}");
                        col.Item().Text($"  Line Total: Rs {line.LineTotal:N2}");
                    }

                    col.Item().PaddingVertical(4).LineHorizontal(1);
                    col.Item().Row(r => { r.RelativeItem().Text("Subtotal:"); r.RelativeItem().AlignRight().Text($"Rs {receipt.Subtotal:N2}"); });
                    col.Item().Row(r => { r.RelativeItem().Text("Sales Tax:"); r.RelativeItem().AlignRight().Text($"Rs {receipt.TaxTotal:N2}"); });
                    col.Item().Row(r => { r.RelativeItem().Text("TOTAL:").Bold(); r.RelativeItem().AlignRight().Text($"Rs {receipt.GrandTotal:N2}").Bold(); });

                    col.Item().PaddingVertical(6).LineHorizontal(1);
                    col.Item().AlignCenter().Text("FBR DIGITAL INVOICE").Bold();
                    col.Item().AlignCenter().Text(receipt.FbrInvoiceNumber).Bold();
                    if (receipt.WasStubbed)
                        col.Item().AlignCenter().Text("(OFFLINE / TEST — not posted to FBR)").FontSize(7);

                    col.Item().AlignCenter().Width(80).Image(qrBytes);
                    col.Item().AlignCenter().Text("Scan to verify on FBR").FontSize(7);
                    col.Item().PaddingTop(8).AlignCenter().Text("Thank you for your purchase!").Italic();
                });
            });
        }).GeneratePdf();
    }

    public async Task SavePdfAsync(ReceiptDataDto receipt, string filePath, CancellationToken ct = default)
    {
        var bytes = GeneratePdf(receipt);
        await File.WriteAllBytesAsync(filePath, bytes, ct);
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..(max - 1)] + "…";
}
