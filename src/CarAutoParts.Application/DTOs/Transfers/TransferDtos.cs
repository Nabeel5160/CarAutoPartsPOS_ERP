using CarAutoParts.Domain.Enums;

namespace CarAutoParts.Application.DTOs.Transfers;

/// <summary>Inventory transfer list row.</summary>
public record TransferListDto(
    int Id,
    string TransferNumber,
    string FromWarehouseName,
    string ToWarehouseName,
    TransferStatus Status,
    DateTime TransferDate);

/// <summary>Inventory transfer detail with lines.</summary>
public record TransferDetailDto(
    int Id,
    string TransferNumber,
    int FromWarehouseId,
    string FromWarehouseName,
    int ToWarehouseId,
    string ToWarehouseName,
    TransferStatus Status,
    DateTime TransferDate,
    string? Notes,
    string? ApprovedBy,
    DateTime? ApprovedAt,
    IReadOnlyList<TransferLineDto> Lines);

/// <summary>Payload for creating an inventory transfer.</summary>
public record TransferCreateDto(
    int FromWarehouseId,
    int ToWarehouseId,
    string? Notes,
    IReadOnlyList<TransferLineDto> Lines);

/// <summary>Inventory transfer line.</summary>
public record TransferLineDto(int ProductId, string? ProductName, decimal Quantity);
