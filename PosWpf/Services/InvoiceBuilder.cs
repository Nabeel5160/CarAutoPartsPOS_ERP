using System.Collections.Generic;
using PosWpf.Models;
using PosWpf.Models.Fbr;

namespace PosWpf.Services;

/// <summary>Maps the POS cart into an FBR Digital Invoicing request payload.</summary>
public static class InvoiceBuilder
{
    public static FbrInvoiceRequest Build(
        IEnumerable<CartItem> cart,
        SellerSettings seller,
        FbrBuyerDetails buyer,
        string posInvoiceRef)
    {
        var registered = buyer.IsRegistered && !string.IsNullOrWhiteSpace(buyer.BuyerNtn);

        var request = new FbrInvoiceRequest
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

        foreach (var item in cart)
        {
            var valueExclST = item.LineSubtotal;
            var tax = item.LineTax;
            request.Items.Add(new FbrInvoiceItem
            {
                HsCode = item.Product.HsCode,
                ProductDescription = item.Product.Name,
                Rate = $"{item.Product.TaxRatePercent:0.##}%",
                UoM = item.Product.UoM,
                Quantity = item.Quantity,
                ValueSalesExcludingST = valueExclST,
                SalesTaxApplicable = tax,
                TotalValues = valueExclST + tax,
                SaleType = buyer.SaleType,
                SroScheduleNo = buyer.SroScheduleNo ?? string.Empty,
                SroItemSerialNo = buyer.SroItemSerialNo ?? string.Empty
            });
        }

        return request;
    }
}
