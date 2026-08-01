using CarAutoParts.Domain.Enums;

namespace CarAutoParts.Application.DTOs.Transfers;

/// <summary>Inventory transfer list row.</summary>
public record TransferListDto(
    int Id,
    string TransferNumber,
    string FromWarehouseName,
    string ToWarehouseName,
    TransferStatus Status,
    DateTime TransferDate,
    bool IsInterBranch = false,
    bool AllLinesPicked = false);

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
    bool IsInterBranch,
    IReadOnlyList<TransferLineDto> Lines,
    bool AllLinesPicked = false);

/// <summary>Payload for creating an inventory transfer.</summary>
public record TransferCreateDto(
    int FromWarehouseId,
    int ToWarehouseId,
    string? Notes,
    IReadOnlyList<TransferLineDto> Lines);

/// <summary>Inventory transfer line.</summary>
public record TransferLineDto(
    int ProductId,
    string? ProductName,
    decimal Quantity,
    decimal ShippedUnitCost = 0,
    int? FromLocationId = null,
    int? ToLocationId = null,
    bool IsPicked = false,
    int? LineId = null);

/// <summary>Confirm pick before ship (Phase 15 P1).</summary>
public record ConfirmTransferPickRequest(IReadOnlyList<ConfirmTransferPickLineRequest>? Lines = null);

public record ConfirmTransferPickLineRequest(int LineId, int? FromLocationId = null, int? ToLocationId = null);
