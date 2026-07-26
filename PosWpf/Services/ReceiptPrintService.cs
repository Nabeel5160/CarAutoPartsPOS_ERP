using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PosWpf.Models;

namespace PosWpf.Services;

/// <summary>Prints a thermal-style receipt with line items and FBR QR code.</summary>
public static class ReceiptPrintService
{
    public static bool Print(ReceiptData receipt)
    {
        var doc = BuildDocument(receipt);
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return false;

        doc.PageHeight = dialog.PrintableAreaHeight;
        doc.PageWidth = dialog.PrintableAreaWidth;
        doc.PagePadding = new Thickness(40);
        doc.ColumnWidth = dialog.PrintableAreaWidth;

        var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
        dialog.PrintDocument(paginator, $"POS Receipt {receipt.FbrInvoiceNumber}");
        return true;
    }

    public static FlowDocument BuildDocument(ReceiptData r)
    {
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            PagePadding = new Thickness(24)
        };

        void AddCenter(string text, double size = 11, bool bold = false)
        {
            doc.Blocks.Add(new Paragraph(new Run(text))
            {
                TextAlignment = TextAlignment.Center,
                FontSize = size,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                Margin = new Thickness(0, 0, 0, 4)
            });
        }

        void AddRow(string left, string right, bool bold = false)
        {
            var p = new Paragraph { Margin = new Thickness(0, 1, 0, 1) };
            p.Inlines.Add(new Run(left) { FontWeight = bold ? FontWeights.Bold : FontWeights.Normal });
            p.Inlines.Add(new Run("  ") { FontWeight = FontWeights.Normal });
            p.Inlines.Add(new Run(right)
            {
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal
            });
            doc.Blocks.Add(p);
        }

        AddCenter(r.Seller.BusinessName, 14, bold: true);
        AddCenter($"NTN/CNIC: {r.Seller.NTNCNIC}");
        AddCenter(r.Seller.Address);
        AddCenter($"{r.Seller.Province}  •  POS {r.Seller.PosId}");
        doc.Blocks.Add(new Paragraph(new Run(new string('─', 42))) { Margin = new Thickness(0, 6, 0, 6) });

        AddCenter("SALES RECEIPT", 12, bold: true);
        AddRow("Date:", r.SaleDate.ToString("dd-MMM-yyyy HH:mm"));
        AddRow("POS Ref:", r.PosRef);
        AddRow("Buyer:", r.BuyerName);
        AddRow("Reg. Type:", r.BuyerRegistrationType);
        if (!string.IsNullOrWhiteSpace(r.BuyerNtn))
            AddRow("Buyer NTN:", r.BuyerNtn);
        if (!string.IsNullOrWhiteSpace(r.ScenarioId))
            AddRow("Scenario:", r.ScenarioId);
        if (!string.IsNullOrWhiteSpace(r.SroScheduleNo))
            AddRow("SRO Schedule:", r.SroScheduleNo);
        if (!string.IsNullOrWhiteSpace(r.SroItemSerialNo))
            AddRow("SRO Item #:", r.SroItemSerialNo);

        doc.Blocks.Add(new Paragraph(new Run(new string('─', 42))) { Margin = new Thickness(0, 6, 0, 6) });

        foreach (var line in r.Lines)
        {
            doc.Blocks.Add(new Paragraph(new Run(Truncate(line.Name, 36)))
            {
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 4, 0, 0)
            });
            var detail = $"  {line.Quantity} x Rs {line.UnitPrice:N2}   Tax Rs {line.LineTax:N2}";
            doc.Blocks.Add(new Paragraph(new Run(detail)));
            doc.Blocks.Add(new Paragraph(new Run($"  Line Total: Rs {line.LineTotal:N2}"))
            {
                Margin = new Thickness(0, 0, 0, 2)
            });
        }

        doc.Blocks.Add(new Paragraph(new Run(new string('─', 42))) { Margin = new Thickness(0, 6, 0, 6) });
        AddRow("Subtotal:", $"Rs {r.Subtotal:N2}");
        AddRow("Sales Tax:", $"Rs {r.TaxTotal:N2}");
        AddRow("TOTAL:", $"Rs {r.GrandTotal:N2}", bold: true);

        doc.Blocks.Add(new Paragraph(new Run(new string('─', 42))) { Margin = new Thickness(0, 8, 0, 8) });
        AddCenter("FBR DIGITAL INVOICE", 11, bold: true);
        AddCenter(r.FbrInvoiceNumber, 10, bold: true);
        if (r.WasStubbed)
            AddCenter("(OFFLINE / TEST — not posted to FBR)");

        var qr = QrCodeHelper.Generate(r.QrPayload, pixelsPerModule: 4);
        var qrImage = new Image
        {
            Source = qr,
            Width = 110,
            Height = 110,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        doc.Blocks.Add(new BlockUIContainer(qrImage) { Margin = new Thickness(0, 8, 0, 4) });
        AddCenter("Scan to verify on FBR", 9);

        doc.Blocks.Add(new Paragraph(new Run("Thank you for your purchase!"))
        {
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 12, 0, 0),
            FontStyle = FontStyles.Italic
        });

        return doc;
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..(max - 1)] + "…";
}
