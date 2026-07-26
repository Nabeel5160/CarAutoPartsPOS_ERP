using CarAutoParts.Application.DTOs.Fbr;
using CarAutoParts.Application.DTOs.Pos;
using CarAutoParts.Application.DTOs.Settings;
using CarAutoParts.Domain.Entities;

namespace CarAutoParts.Application.Services;

/// <summary>Builds FBR invoice payloads from POS checkout data.</summary>
public static class FbrInvoiceBuilder
{
    /// <summary>Maps a sales invoice and line items to an FBR request.</summary>
    public static FbrInvoiceRequestDto Build(
        SalesInvoice invoice,
        IReadOnlyList<SalesInvoiceLine> lines,
        CompanySettings settings,
        PosBuyerDto? buyer,
        string? scenarioId,
        string? saleType)
    {
        var buyerDetails = buyer != null
            ? new FbrBuyerDetailsDto(
                buyer.Name,
                buyer.NtnCnic,
                buyer.RegistrationType,
                buyer.Province,
                buyer.Address,
                scenarioId,
                buyer.SroScheduleNo ?? string.Empty,
                buyer.SroItemSerialNo ?? string.Empty,
                saleType ?? "Goods at standard rate (default)")
            : new FbrBuyerDetailsDto(
                invoice.BuyerName ?? "Walk-in Customer",
                invoice.BuyerNtnCnic,
                invoice.BuyerRegistrationType ?? "Unregistered",
                invoice.BuyerProvince ?? settings.City ?? string.Empty,
                invoice.BuyerAddress ?? settings.Address ?? string.Empty,
                scenarioId,
                string.Empty,
                string.Empty,
                saleType ?? "Goods at standard rate (default)");

        var registered = buyerDetails.IsRegistered && !string.IsNullOrWhiteSpace(buyerDetails.BuyerNtn);

        var request = new FbrInvoiceRequestDto
        {
            InvoiceType = "Sale Invoice",
            InvoiceDate = invoice.InvoiceDate.ToString("yyyy-MM-dd"),
            SellerNTNCNIC = settings.Ntn ?? string.Empty,
            SellerBusinessName = settings.CompanyName,
            SellerProvince = settings.City ?? string.Empty,
            SellerAddress = settings.Address ?? string.Empty,
            BuyerNTNCNIC = registered ? buyerDetails.BuyerNtn?.Trim() : null,
            BuyerBusinessName = string.IsNullOrWhiteSpace(buyerDetails.BuyerName)
                ? "Walk-in Customer"
                : buyerDetails.BuyerName.Trim(),
            BuyerProvince = string.IsNullOrWhiteSpace(buyerDetails.BuyerProvince)
                ? settings.City ?? string.Empty
                : buyerDetails.BuyerProvince,
            BuyerAddress = string.IsNullOrWhiteSpace(buyerDetails.BuyerAddress)
                ? settings.Address ?? string.Empty
                : buyerDetails.BuyerAddress,
            BuyerRegistrationType = buyerDetails.BuyerRegistrationType,
            InvoiceRefNo = invoice.InvoiceNumber,
            ScenarioId = string.IsNullOrWhiteSpace(scenarioId) ? null : scenarioId.Trim()
        };

        foreach (var line in lines)
        {
            var valueExclSt = line.LineTotal - line.TaxAmount;
            request.Items.Add(new FbrInvoiceItemDto
            {
                HsCode = line.HsCode ?? string.Empty,
                ProductDescription = line.ProductName,
                Rate = $"{line.TaxRate:0.##}%",
                UoM = line.UnitOfMeasure ?? "PCS",
                Quantity = line.Quantity,
                ValueSalesExcludingST = valueExclSt,
                SalesTaxApplicable = line.TaxAmount,
                TotalValues = line.LineTotal,
                SaleType = buyerDetails.SaleType,
                SroScheduleNo = buyerDetails.SroScheduleNo,
                SroItemSerialNo = buyerDetails.SroItemSerialNo
            });
        }

        return request;
    }
}
