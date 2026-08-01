# Inventory costing

## Default method

New inventory rows use **weighted average** cost unless company settings or the product require otherwise.

- Company setting `DefaultValuationMethod` applies when an `InventoryItem` is first created for a product/warehouse.
- Per-item `ValuationMethod` can still be `Average` or `Fifo`.

## Average (default)

On receive, average cost is recalculated as:

`newAvg = (onHand * avgCost + qty * unitCost) / (onHand + qty)`

Issues (sales, transfers out, purchase returns) use the current `AverageCost` on the inventory item.

## FIFO

Used when `ValuationMethod = Fifo` **or** the product has `TrackBatches = true`.

- Receives create/consume `StockBatch` rows.
- Issues consume oldest batches first at batch unit cost.
- Negative stock (when allowed by policy) is not supported on the FIFO path without sufficient batch quantity.

## Valuation reports (Phase 7)

`GET /api/inventory/value?method=Average|Fifo&warehouseId=&branchId=`

| Method | Formula |
|--------|---------|
| **Average** (default) | Σ `QuantityOnHand × AverageCost` |
| **FIFO** | Σ `StockBatch.QuantityRemaining × UnitCost` (+ residual qty without batches uses AverageCost) |

Sales invoice lines store `UnitCost` at issue for margin/Insights (fallback: product CostPrice / PurchasePrice).

Dashboard inventory-value KPIs remain average-based; use the inventory value API or Inventory page toggle for FIFO.

## Changing method

Changing `DefaultValuationMethod` only affects **new** inventory items. Existing items keep their method. There is no mass revaluation in Phase 4/7.
