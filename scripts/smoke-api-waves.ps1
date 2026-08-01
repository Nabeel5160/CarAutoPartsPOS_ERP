# Waves B-H API smoke (GET-first). See docs/SMOKE-INTEGRATION-PLAN.md
# Does not replace scripts/smoke-money-path.ps1 (money-path baseline).
# Compatible with Windows PowerShell 5.1 and PowerShell 7+.
$ErrorActionPreference = 'Continue'
$base = $env:CAP_API_BASE
if ([string]::IsNullOrWhiteSpace($base)) { $base = 'http://127.0.0.1:5280' }

$pass = 0; $fail = 0; $skip = 0
$results = New-Object System.Collections.Generic.List[object]
$waveStats = @{}

function Record($wave, $name, $status, $code, $detail = '') {
  $script:results.Add([pscustomobject]@{ Wave = $wave; Name = $name; Status = $status; Code = $code; Detail = $detail })
  if (-not $script:waveStats.ContainsKey($wave)) {
    $script:waveStats[$wave] = @{ Pass = 0; Fail = 0; Skip = 0 }
  }
  switch ($status) {
    'PASS' { $script:pass++; $script:waveStats[$wave].Pass++ }
    'FAIL' { $script:fail++; $script:waveStats[$wave].Fail++ }
    'SKIP' { $script:skip++; $script:waveStats[$wave].Skip++ }
  }
  $color = switch ($status) { 'PASS' { 'Green' } 'FAIL' { 'Red' } default { 'Yellow' } }
  Write-Host "$status [$wave] $name status=$code $detail" -ForegroundColor $color
}

function Api($method, $path, $headers = @{}, $body = $null) {
  try {
    $p = @{ Uri = "$base$path"; Method = $method; Headers = $headers; UseBasicParsing = $true }
    if ($null -ne $body) {
      $p.ContentType = 'application/json'
      $p.Body = if ($body -is [string]) { $body } else { ($body | ConvertTo-Json -Depth 8 -Compress) }
    }
    $r = Invoke-WebRequest @p
    return @{ S = [int]$r.StatusCode; C = $r.Content }
  } catch {
    $st = 0; $c = $_.Exception.Message
    if ($_.Exception.Response) {
      $st = [int]$_.Exception.Response.StatusCode
      try { $c = [IO.StreamReader]::new($_.Exception.Response.GetResponseStream()).ReadToEnd() } catch {}
    }
    return @{ S = $st; C = $c }
  }
}

function Expect2xx($wave, $name, $method, $path, $headers) {
  $r = Api $method $path $headers
  if ($r.S -ge 200 -and $r.S -lt 300) {
    Record $wave $name 'PASS' $r.S
  } else {
    $raw = if ($r.C) { $r.C.ToString() -replace '\s+', ' ' } else { '' }
    $snip = if ($raw.Length -gt 120) { $raw.Substring(0, 120) } else { $raw }
    Record $wave $name 'FAIL' $r.S $snip
  }
  return $r
}

Write-Host "API waves smoke against $base" -ForegroundColor Cyan
$today = (Get-Date).ToString('yyyy-MM-dd')
$qFromTo = ('?from=' + $today + '&to=' + $today)
$h = @{}

# ----- Wave B: health / auth -----
$live = Api GET '/health/live'
if ($live.S -ne 200) { $live = Api GET '/health' }
if ($live.S -eq 200) { Record 'B' 'Health' 'PASS' $live.S } else { Record 'B' 'Health' 'FAIL' $live.S }

$token = $env:CAP_SMOKE_TOKEN
if ([string]::IsNullOrWhiteSpace($token)) {
  $login = Api POST '/api/auth/login' @{} @{ username = 'admin'; password = 'admin123' }
  $j = $null; try { $j = $login.C | ConvertFrom-Json } catch {}
  if ($login.S -eq 200 -and $j.accessToken) {
    $token = $j.accessToken
    Record 'B' 'Login' 'PASS' $login.S ("mfa=" + $j.mfaRequired)
  } elseif ($j.mfaRequired -or $j.mfaTicket) {
    Record 'B' 'Login' 'SKIP' $login.S 'MFA required - set CAP_SMOKE_TOKEN or disable MFA for smoke'
  } else {
    Record 'B' 'Login' 'FAIL' $login.S
  }
} else {
  Record 'B' 'Login' 'SKIP' 0 'using CAP_SMOKE_TOKEN'
}

if (-not $token) {
  Write-Host ("Cannot continue waves C-H without token. PASS=" + $pass + " FAIL=" + $fail + " SKIP=" + $skip) -ForegroundColor Red
  Write-Host "WAVE_SUMMARY incomplete"
  exit 1
}

$h = @{ Authorization = ("Bearer " + $token) }
Expect2xx 'B' 'Me' GET '/api/auth/me' $h | Out-Null
Expect2xx 'B' 'MfaStatus' GET '/api/auth/mfa/status' $h | Out-Null

# ----- Wave C: catalog / inventory -----
$waveC = @(
  @{ N = 'Products'; P = '/api/products?page=1&pageSize=5' },
  @{ N = 'Categories'; P = '/api/categories' },
  @{ N = 'Brands'; P = '/api/brands' },
  @{ N = 'Warehouses'; P = '/api/warehouses' },
  @{ N = 'Inventory'; P = '/api/inventory?page=1&pageSize=5' },
  @{ N = 'InventoryMovements'; P = '/api/inventory/movements?page=1&pageSize=5' },
  @{ N = 'LowStock'; P = '/api/inventory/alerts/low-stock' },
  @{ N = 'Overstock'; P = '/api/inventory/alerts/overstock' },
  @{ N = 'InventoryValue'; P = '/api/inventory/value?method=Average' },
  @{ N = 'SerialNumbers'; P = '/api/serial-numbers?page=1&pageSize=5' },
  @{ N = 'Transfers'; P = '/api/transfers?page=1&pageSize=5' },
  @{ N = 'Reservations'; P = '/api/enterprise/reservations' },
  @{ N = 'CycleCounts'; P = '/api/enterprise/cycle-counts' },
  @{ N = 'Kits'; P = '/api/enterprise/kits' },
  @{ N = 'PriceLists'; P = '/api/enterprise/price-lists' },
  @{ N = 'FitmentOptions'; P = '/api/products/fitment-options' }
)
foreach ($c in $waveC) { Expect2xx 'C' $c.N GET $c.P $h | Out-Null }

# ----- Wave D: POS / sales / wholesale -----
$waveD = @(
  @{ N = 'PosProducts'; P = '/api/pos/products?search=SKU' },
  @{ N = 'PosFitment'; P = '/api/pos/fitment-options' },
  @{ N = 'PosHolds'; P = '/api/pos/holds' },
  @{ N = 'PosShiftCurrent'; P = '/api/pos/shifts/current' },
  @{ N = 'PosTills'; P = '/api/pos/tills' },
  @{ N = 'PosXReport'; P = '/api/pos/shifts/x-report' },
  @{ N = 'SalesInvoices'; P = '/api/sales/invoices?page=1&pageSize=5' },
  @{ N = 'SalesOrders'; P = '/api/sales/orders?page=1&pageSize=5' },
  @{ N = 'ReturnsSales'; P = '/api/returns/sales?page=1&pageSize=5' },
  @{ N = 'Quotations'; P = '/api/enterprise/quotations' },
  @{ N = 'WholesaleOrders'; P = '/api/enterprise/sales-orders' },
  @{ N = 'Deliveries'; P = '/api/enterprise/deliveries' },
  @{ N = 'FbrMetrics'; P = '/api/enterprise/fbr/metrics' },
  @{ N = 'FbrSubmissions'; P = '/api/enterprise/fbr/submissions' },
  @{ N = 'Customers'; P = '/api/customers?page=1&pageSize=5' },
  @{ N = 'Suppliers'; P = '/api/suppliers?page=1&pageSize=5' }
)
foreach ($c in $waveD) {
  if ($c.N -eq 'PosXReport') {
    $xr = Api GET $c.P $h
    if ($xr.S -ge 200 -and $xr.S -lt 300) { Record 'D' $c.N 'PASS' $xr.S }
    elseif ($xr.S -eq 400) { Record 'D' $c.N 'SKIP' $xr.S 'no open shift (expected)' }
    else { Record 'D' $c.N 'FAIL' $xr.S }
  } else {
    Expect2xx 'D' $c.N GET $c.P $h | Out-Null
  }
}

# ----- Wave E: purchasing -----
$waveE = @(
  @{ N = 'PurchaseOrders'; P = '/api/purchase-orders?page=1&pageSize=5' },
  @{ N = 'Requisitions'; P = '/api/purchase-requisitions' },
  @{ N = 'ReorderSuggestions'; P = '/api/reorder/suggestions' },
  @{ N = 'Grn'; P = '/api/enterprise/grn' },
  @{ N = 'ApInvoices'; P = '/api/enterprise/ap-invoices' }
)
foreach ($c in $waveE) { Expect2xx 'E' $c.N GET $c.P $h | Out-Null }

# ----- Wave F: finance -----
$waveF = @(
  @{ N = 'Companies'; P = '/api/finance/companies' },
  @{ N = 'Coa'; P = '/api/finance/coa' },
  @{ N = 'Periods'; P = '/api/finance/periods' },
  @{ N = 'Journals'; P = '/api/finance/journals?page=1&pageSize=5' },
  @{ N = 'OpeningBalances'; P = '/api/finance/opening-balances' },
  @{ N = 'BankStatements'; P = '/api/finance/bank-statements' },
  @{ N = 'UnclearedGl'; P = '/api/finance/bank-statements/uncleared-gl' },
  @{ N = 'AccountMappings'; P = '/api/enterprise/account-mappings' },
  @{ N = 'AgingCustomers'; P = '/api/enterprise/aging/customers' },
  @{ N = 'AgingSuppliers'; P = '/api/enterprise/aging/suppliers' },
  @{ N = 'TrialBalance'; P = '/api/enterprise/reports/trial-balance' },
  @{ N = 'ProfitLoss'; P = ('/api/enterprise/reports/profit-loss' + $qFromTo) },
  @{ N = 'BalanceSheet'; P = ('/api/enterprise/reports/balance-sheet?asOf=' + $today) }
)
foreach ($c in $waveF) { Expect2xx 'F' $c.N GET $c.P $h | Out-Null }

$cos = Api GET '/api/finance/companies' $h
if ($cos.S -ge 200 -and $cos.S -lt 300) {
  try {
    $clist = $cos.C | ConvertFrom-Json
    $cid = $null
    if ($clist -is [array] -and $clist.Count -gt 0) { $cid = $clist[0].id }
    elseif ($clist.id) { $cid = $clist.id }
    if ($cid) { Expect2xx 'F' 'CompanyBranches' GET ("/api/finance/companies/$cid/branches") $h | Out-Null }
    else { Record 'F' 'CompanyBranches' 'SKIP' 0 'no company id' }
  } catch { Record 'F' 'CompanyBranches' 'SKIP' 0 'parse companies' }
} else {
  Record 'F' 'CompanyBranches' 'SKIP' $cos.S 'companies failed'
}

# ----- Wave G: reports -----
$waveG = @(
  @{ N = 'DailySales'; P = ('/api/reports/daily-sales' + $qFromTo) },
  @{ N = 'Sales'; P = ('/api/reports/sales' + $qFromTo) },
  @{ N = 'InventoryRpt'; P = '/api/reports/inventory' },
  @{ N = 'PurchasesRpt'; P = ('/api/reports/purchases' + $qFromTo) },
  @{ N = 'Profit'; P = ('/api/reports/profit' + $qFromTo) },
  @{ N = 'SalesReturns'; P = ('/api/reports/sales-returns' + $qFromTo) },
  @{ N = 'ZShifts'; P = ('/api/reports/z-shifts' + $qFromTo) },
  @{ N = 'SalesDim'; P = ('/api/reports/sales-dim' + $qFromTo) },
  @{ N = 'SalesStaff'; P = ('/api/reports/sales-staff' + $qFromTo) },
  @{ N = 'MovementsRpt'; P = ('/api/reports/movements' + $qFromTo) },
  @{ N = 'PurchasingPipeline'; P = '/api/reports/purchasing-pipeline' },
  @{ N = 'AgingRpt'; P = '/api/reports/aging' },
  @{ N = 'Tax'; P = ('/api/reports/tax' + $qFromTo) },
  @{ N = 'FbrRpt'; P = ('/api/reports/fbr' + $qFromTo) },
  @{ N = 'StockAging'; P = '/api/reports/stock-aging' },
  @{ N = 'SkuMargin'; P = ('/api/reports/sku-margin' + $qFromTo) },
  @{ N = 'AnalyticsExport'; P = '/api/reports/analytics-export' }
)
foreach ($c in $waveG) { Expect2xx 'G' $c.N GET $c.P $h | Out-Null }

# ----- Wave H: system / governance -----
$waveH = @(
  @{ N = 'Dashboard'; P = '/api/dashboard' },
  @{ N = 'Analytics'; P = '/api/analytics' },
  @{ N = 'Users'; P = '/api/users' },
  @{ N = 'Roles'; P = '/api/roles' },
  @{ N = 'Settings'; P = '/api/settings' },
  @{ N = 'AppConfig'; P = '/api/app-config' },
  @{ N = 'OnboardingStatus'; P = '/api/onboarding/status' },
  @{ N = 'ApprovalsPending'; P = '/api/approvals/pending' },
  @{ N = 'ApprovalsPolicies'; P = '/api/approvals/policies' },
  @{ N = 'AuditLogs'; P = '/api/audit-logs?page=1&pageSize=5' },
  @{ N = 'Backups'; P = '/api/backups' },
  @{ N = 'Notifications'; P = '/api/notifications' },
  @{ N = 'UnreadCount'; P = '/api/notifications/unread-count' },
  @{ N = 'AppConfigPublic'; P = '/api/app-config/public' }
)
foreach ($c in $waveH) { Expect2xx 'H' $c.N GET $c.P $h | Out-Null }

Write-Host ""
Write-Host "=== WAVE SUMMARY ===" -ForegroundColor Cyan
foreach ($w in @('B','C','D','E','F','G','H')) {
  $s = $waveStats[$w]
  if (-not $s) { continue }
  $label = if ($s.Fail -gt 0) { 'FAIL' } else { 'PASS' }
  Write-Host ("Wave $w : $label  pass=$($s.Pass) fail=$($s.Fail) skip=$($s.Skip)")
}
Write-Host ("TOTAL PASS=$pass FAIL=$fail SKIP=$skip")
if ($fail -gt 0) { exit 1 } else { exit 0 }
