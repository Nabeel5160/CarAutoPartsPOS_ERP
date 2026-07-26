namespace CarAutoParts.Application.DTOs;

/// <summary>Shared lookup item for dropdowns and selectors.</summary>
public record LookupItemDto(int Id, string Name);

/// <summary>Id and display label pair.</summary>
public record IdNameDto(int Id, string Name);

/// <summary>Monetary summary used across reports and dashboards.</summary>
public record MoneySummaryDto(decimal Amount, string Currency = "PKR");
