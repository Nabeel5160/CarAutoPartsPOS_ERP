using CarAutoParts.Domain.Services;
using FluentAssertions;
using Xunit;

namespace CarAutoParts.Domain.Tests;

public class ThreeWayMatchInvariantTests
{
    [Fact]
    public void Three_way_requires_po_grn_invoice_equal()
    {
        ThreeWayMatchRules.LineMatched(true, 5, 5, 5).Should().BeTrue();
        ThreeWayMatchRules.LineMatched(true, 5, 4, 5).Should().BeFalse();
        ThreeWayMatchRules.LineMatched(true, 0, 5, 5).Should().BeFalse();
    }

    [Fact]
    public void Two_way_without_po_matches_grn_to_invoice()
    {
        ThreeWayMatchRules.LineMatched(false, 0, 5, 5).Should().BeTrue();
        ThreeWayMatchRules.LineMatched(false, 0, 4, 5).Should().BeFalse();
    }

    [Fact]
    public void Qty_tolerance_allows_small_variance()
    {
        ThreeWayMatchRules.LineMatched(true, 100, 102, 101, qtyTolerancePercent: 2).Should().BeTrue();
        ThreeWayMatchRules.LineMatched(true, 100, 105, 100, qtyTolerancePercent: 2).Should().BeFalse();
    }

    [Fact]
    public void Price_tolerance_and_max_receivable()
    {
        ThreeWayMatchRules.PriceMatched(100, 101, 1).Should().BeTrue();
        ThreeWayMatchRules.PriceMatched(100, 103, 1).Should().BeFalse();
        ThreeWayMatchRules.MaxReceivableQty(10, 8, 0).Should().Be(2);
        ThreeWayMatchRules.MaxReceivableQty(10, 8, 10).Should().Be(3); // cap 11 - 8
    }
}
