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
- **Graphs | Numbers** toggle (`CapViewToggle`): Graphs shows CapChart overlays; Numbers shows the prior KPIs/tables. Defaults: Dashboard → Graphs; Reports / Analytics / Financial → Numbers.
- Sales-dim: click a doughnut segment to filter the table client-side (switch to Numbers to audit filtered rows).
- CapChart **PNG** button uses ECharts `getDataURL`.

## Verify

1. Run API + Web; open Dashboard — Graphs tab shows six chart cards; Numbers shows KPI cards only.
2. Toggle theme — charts recolor without reload.
3. Reports → Daily sales / Sales dim / Staff / Profit dim / Tax / Stock age — Numbers default (tables); switch Graphs for charts; export still works.
4. Sales dim: click a segment on Graphs → switch Numbers → filtered table; clear filter restores rows.
5. Analytics / Financial P&amp;L — Numbers default; Graphs shows charts.
