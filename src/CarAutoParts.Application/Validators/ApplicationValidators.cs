using CarAutoParts.Application.DTOs.Auth;
using CarAutoParts.Application.DTOs.Inventory;
using CarAutoParts.Application.DTOs.Partners;
using CarAutoParts.Application.DTOs.Pos;
using CarAutoParts.Application.DTOs.Purchases;
using FluentValidation;

namespace CarAutoParts.Application.Validators;

/// <summary>Validates user create/update payloads.</summary>
public class UserCreateValidator : AbstractValidator<UserCreateDto>
{
    public UserCreateValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(50);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6).When(x => x.Password != null);
    }
}

/// <summary>Validates stock adjustment requests.</summary>
public class StockAdjustmentValidator : AbstractValidator<StockAdjustmentDto>
{
    public StockAdjustmentValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.QuantityDelta).NotEqual(0);
    }
}

/// <summary>Validates supplier payloads.</summary>
public class SupplierValidator : AbstractValidator<SupplierDto>
{
    public SupplierValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

/// <summary>Validates customer payloads.</summary>
public class CustomerValidator : AbstractValidator<CustomerDto>
{
    public CustomerValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

/// <summary>Validates purchase order create payloads.</summary>
public class PurchaseOrderValidator : AbstractValidator<PurchaseOrderCreateDto>
{
    public PurchaseOrderValidator()
    {
        RuleFor(x => x.SupplierId).GreaterThan(0);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).GreaterThan(0);
            line.RuleFor(l => l.QuantityOrdered).GreaterThan(0);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0);
        });
    }
}

/// <summary>Validates POS checkout payloads.</summary>
public class PosCheckoutValidator : AbstractValidator<PosCheckoutDto>
{
    public PosCheckoutValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.PaymentMethod)
            .NotEmpty()
            .When(x => x.Tenders is null || x.Tenders.Count == 0);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
        });
    }
}
