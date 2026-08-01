# Animated charts (Apache ECharts)

Car Auto Parts POS uses a shared **CapChart** facade over Apache ECharts for Dashboard, Reports, Analytics, and Financial P&amp;L overlays.

## Stack

| Piece | Role |
|-------|------|
| `wwwroot/js/capCharts.js` | Lazy-loads ECharts (+ `echarts-gl` only for `bar3d`); `render` / `update` / `destroy` / `rethemeAll` / `setFrame` / `exportPng` / `onClick` |
| `Components/CapChart.razor` | Div host, theme recolor via `ThemeService.Changed`, PNG export, optional click callback |
| `Components/CapChartPlayer.razor` | Play / pause / speed / scrub for `Frames[]` timelines (`prefers-reduced-motion` disables autoplay) |
| `Models/CapChartModels.cs` | Spec + `CapChartFactory` helpers (`Line`, `DualBar`, `Doughnut`, `Timeline`, `Bar3D`, …) |

Theme colors come from CSS tokens: `--cap-accent`, `--cap-accent-2`, `--cap-text`, `--cap-muted`, `--cap-border`, `--cap-surface`.

## Dashboard

- KPI series deserialized on the Web `DashboardDto` (`MonthlySales`, `InventoryTrend`, `TopProducts`, `CategoryDistribution`).
- Charts: sales vs purchases, category doughnut, top products, inventory trend.
- **4D Sales Pulse** + **Executive 3D** from `GET /api/dashboard/timeline?from&to&grain=day|week&groupBy=category|branch` (same branch ACL as dashboard).

## Reports / polish

- Chart overlays sit **above** existing tables; Excel/PDF export paths are unchanged.
- Sales-dim: click a doughnut segment to filter the table client-side.
- CapChart **PNG** button uses ECharts `getDataURL`.

## Verify

1. Run API + Web; open Dashboard — six chart cards (including pulse + 3D) after data loads.
2. Toggle theme — charts recolor without reload.
3. Reports → Daily sales / Sales dim / Staff / Profit dim / Tax / Stock age — charts appear above grids; export still works.
4. Sales dim: click a segment → table filters; clear filter button restores rows.
