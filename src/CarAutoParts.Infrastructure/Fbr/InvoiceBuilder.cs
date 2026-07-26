using CarAutoParts.Application.DTOs.Fbr;

namespace CarAutoParts.Infrastructure.Fbr;

/// <summary>Maps POS cart lines into an FBR Digital Invoicing request payload.</summary>
public static class InvoiceBuilder
{
    public static FbrInvoiceRequestDto Build(
        IEnumerable<FbrInvoiceLineDto> lines,
        FbrSellerSettingsDto seller,
        FbrBuyerDetailsDto buyer,
        string posInvoiceRef)
    {
        var registered = buyer.IsRegistered && !string.IsNullOrWhiteSpace(buyer.BuyerNtn);

        var request = new FbrInvoiceRequestDto
        {
            InvoiceType = "Sale Invoice",
            InvoiceDate = DateTime.Now.ToString("yyyy-MM-dd"),
            SellerNTNCNIC = seller.NTNCNIC,
            SellerBusinessName = seller.BusinessName,
            SellerProvince = seller.Province,
            SellerAddress = seller.Address,
            BuyerNTNCNIC = registered ? buyer.BuyerNtn?.Trim() : null,
            BuyerBusinessName = string.IsNullOrWhiteSpace(buyer.BuyerName) ? "Walk-in Customer" : buyer.BuyerName.Trim(),
            BuyerProvince = string.IsNullOrWhiteSpace(buyer.BuyerProvince) ? seller.Province : buyer.BuyerProvince,
            BuyerAddress = string.IsNullOrWhiteSpace(buyer.BuyerAddress) ? seller.Address : buyer.BuyerAddress,
            BuyerRegistrationType = buyer.BuyerRegistrationType,
            InvoiceRefNo = posInvoiceRef,
            ScenarioId = string.IsNullOrWhiteSpace(buyer.ScenarioId) ? null : buyer.ScenarioId.Trim()
        };

        foreach (var item in lines)
        {
            var valueExclSt = item.LineSubtotal;
            var tax = item.LineTax;
            request.Items.Add(new FbrInvoiceItemDto
            {
                HsCode = item.HsCode,
                ProductDescription = item.ProductName,
                Rate = $"{item.TaxRatePercent:0.##}%",
                UoM = item.UnitOfMeasure,
                Quantity = item.Quantity,
                ValueSalesExcludingST = valueExclSt,
                SalesTaxApplicable = tax,
                TotalValues = valueExclSt + tax,
                SaleType = buyer.SaleType,
                SroScheduleNo = buyer.SroScheduleNo,
                SroItemSerialNo = buyer.SroItemSerialNo
            });
        }

        return request;
    }
}
