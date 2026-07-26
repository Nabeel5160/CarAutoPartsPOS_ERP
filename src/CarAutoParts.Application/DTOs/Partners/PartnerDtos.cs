using CarAutoParts.Domain.Enums;

namespace CarAutoParts.Application.DTOs.Partners;

/// <summary>Supplier list row.</summary>
public record SupplierDto(
    int Id,
    string Name,
    string? Company,
    string? City,
    string? Phone,
    string? Email,
    decimal Balance,
    bool IsActive);

/// <summary>Supplier detail with contact and tax info.</summary>
public record SupplierDetailDto(
    int Id,
    string Name,
    string? Company,
    string? Address,
    string? City,
    string? Email,
    string? Phone,
    string? Ntn,
    string? Strn,
    decimal Balance,
    bool IsActive);

/// <summary>Supplier ledger entry.</summary>
public record SupplierLedgerEntryDto(
    DateTime Date,
    string Description,
    string? Reference,
    decimal Debit,
    decimal Credit,
    decimal Balance);

/// <summary>Customer list row.</summary>
public record CustomerDto(
    int Id,
    string Name,
    CustomerType CustomerType,
    string? Phone,
    string? Email,
    decimal Balance,
    decimal CreditLimit,
    bool IsActive);

/// <summary>Customer detail.</summary>
public record CustomerDetailDto(
    int Id,
    string Name,
    CustomerType CustomerType,
    string? Phone,
    string? Email,
    string? Address,
    string? NtnCnic,
    string? Province,
    decimal CreditLimit,
    decimal Balance,
    bool IsActive);

/// <summary>Customer ledger entry.</summary>
public record CustomerLedgerEntryDto(
    DateTime Date,
    string Description,
    string? Reference,
    decimal Debit,
    decimal Credit,
    decimal Balance);
