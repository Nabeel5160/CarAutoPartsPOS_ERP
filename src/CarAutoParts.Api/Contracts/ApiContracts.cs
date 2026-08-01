using CarAutoParts.Application.DTOs.Auth;

namespace CarAutoParts.Api.Contracts;

public record LoginRequest(string Username, string Password);

public record ApiLoginResponse(
    string? AccessToken,
    DateTime? ExpiresAt,
    LoginResultDto? User,
    bool MfaRequired = false,
    string? MfaTicket = null,
    bool MustEnrollMfa = false);

public record ReceiveStockRequest(
    int ProductId,
    int WarehouseId,
    decimal Quantity,
    decimal UnitCost,
    string? BatchNumber);

public record DeductStockRequest(
    int ProductId,
    int WarehouseId,
    decimal Quantity,
    string ReferenceType,
    int ReferenceId);

public record ReturnStockRequest(
    int ProductId,
    int WarehouseId,
    decimal Quantity,
    string ReferenceType,
    int ReferenceId);

public record RegisterSerialRequest(int ProductId, string Serial, int WarehouseId);

public record CreateNotificationRequest(
    string Type,
    string Title,
    string Message,
    string? EntityType,
    int? EntityId);

public record CreateBackupRequest(bool IsAutomatic = false);
