namespace CarAutoParts.Domain.Services;

/// <summary>Quantity/price match rules for purchase invoice posting (3-way when PO present).</summary>
public static class ThreeWayMatchRules
{
    public static bool LineMatched(bool hasPurchaseOrder, decimal poQty, decimal grnQty, decimal invoiceQty) =>
        LineMatched(hasPurchaseOrder, poQty, grnQty, invoiceQty, qtyTolerancePercent: 0);

    public static bool LineMatched(
        bool hasPurchaseOrder,
        decimal poQty,
        decimal grnQty,
        decimal invoiceQty,
        decimal qtyTolerancePercent)
    {
        if (hasPurchaseOrder)
            return WithinTolerance(poQty, grnQty, qtyTolerancePercent)
                   && WithinTolerance(grnQty, invoiceQty, qtyTolerancePercent);
        return WithinTolerance(grnQty, invoiceQty, qtyTolerancePercent);
    }

    public static bool PriceMatched(decimal expectedUnitCost, decimal invoiceUnitCost, decimal priceTolerancePercent) =>
        WithinTolerance(expectedUnitCost, invoiceUnitCost, priceTolerancePercent);

    public static bool WithinTolerance(decimal expected, decimal actual, decimal tolerancePercent)
    {
        if (expected == actual) return true;
        if (tolerancePercent <= 0) return false;
        var basis = Math.Abs(expected) < 0.0000001m ? Math.Abs(actual) : Math.Abs(expected);
        if (basis < 0.0000001m) return actual == 0;
        var delta = Math.Abs(expected - actual);
        return delta <= basis * (tolerancePercent / 100m);
    }

    /// <summary>Max receivable qty given ordered and already received, with over-receive %.</summary>
    public static decimal MaxReceivableQty(decimal ordered, decimal alreadyReceived, decimal overReceivePercent)
    {
        var cap = ordered * (1m + Math.Max(0, overReceivePercent) / 100m);
        return Math.Max(0, cap - alreadyReceived);
    }
}
