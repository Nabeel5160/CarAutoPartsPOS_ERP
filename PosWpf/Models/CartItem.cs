using PosWpf.Common;

namespace PosWpf.Models;

/// <summary>
/// A line in the cart: a product plus a quantity. Exposes computed money fields.
/// </summary>
public class CartItem : ObservableObject
{
    private int _quantity = 1;

    public required Product Product { get; init; }

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (value < 1) value = 1;
            if (SetProperty(ref _quantity, value))
            {
                OnPropertyChanged(nameof(LineSubtotal));
                OnPropertyChanged(nameof(LineTax));
                OnPropertyChanged(nameof(LineTotal));
            }
        }
    }

    /// <summary>Price * quantity, excluding sales tax.</summary>
    public decimal LineSubtotal => Product.UnitPrice * Quantity;

    /// <summary>Sales tax amount for this line.</summary>
    public decimal LineTax => Math.Round(LineSubtotal * (Product.TaxRatePercent / 100m), 2);

    /// <summary>Subtotal + tax.</summary>
    public decimal LineTotal => LineSubtotal + LineTax;
}
