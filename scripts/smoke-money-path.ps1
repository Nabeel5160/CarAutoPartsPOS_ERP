$ErrorActionPreference = 'Continue'
$base = $env:CAP_API_BASE
if ([string]::IsNullOrWhiteSpace($base)) { $base = 'http://127.0.0.1:5280' }

$pass = 0; $fail = 0
function Assert($name, $ok, $detail = '') {
  if ($ok) { $script:pass++; Write-Host "PASS $name $detail" -ForegroundColor Green }
  else { $script:fail++; Write-Host "FAIL $name $detail" -ForegroundColor Red }
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

Write-Host "Smoke against $base" -ForegroundColor Cyan

$live = Api GET '/health/live'
if ($live.S -ne 200) { $live = Api GET '/health' }
Assert 'Health' ($live.S -eq 200) "status=$($live.S)"

$login = Api POST '/api/auth/login' @{} @{ username = 'admin'; password = 'admin123' }
$j = $null; try { $j = $login.C | ConvertFrom-Json } catch {}
$token = $j.accessToken
Assert 'Login' ($login.S -eq 200 -and $token) "mfa=$($j.mfaRequired)"

if (-not $token) {
  Write-Host "Cannot continue without token (MFA enrolled?). PASS=$pass FAIL=$fail"
  exit 1
}

$h = @{ Authorization = "Bearer $token" }
Assert 'Me' ((Api GET '/api/auth/me' $h).S -eq 200)
Assert 'Dashboard' ((Api GET '/api/dashboard' $h).S -eq 200)
Assert 'COA' ((Api GET '/api/finance/coa' $h).S -eq 200)
Assert 'Journals' ((Api GET '/api/finance/journals?page=1&pageSize=5' $h).S -eq 200)
Assert 'Analytics' ((Api GET '/api/analytics' $h).S -eq 200)
Assert 'Onboarding status' ((Api GET '/api/onboarding/status' $h).S -eq 200)
Assert 'Approval policies' ((Api GET '/api/approvals/policies' $h).S -eq 200)
Assert 'Inventory value' ((Api GET '/api/inventory/value?method=Average' $h).S -eq 200)

# Phase 19 — timed POS search + day sales (warm budgets; see docs/PERFORMANCE.md)
function Timed($name, $path) {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $r = Api GET $path $h
  $sw.Stop()
  $ms = [int]$sw.ElapsedMilliseconds
  Assert $name ($r.S -eq 200) "status=$($r.S) elapsedMs=$ms"
  Write-Host "TIME $name ${ms}ms (budget: see docs/PERFORMANCE.md)" -ForegroundColor Cyan
  return $ms
}

$today = (Get-Date).ToString('yyyy-MM-dd')
Timed 'POS products search' '/api/pos/products?search=SKU'
Timed 'Daily sales report' "/api/reports/daily-sales?from=$today&to=$today"

Write-Host "`nPASS=$pass FAIL=$fail"
if ($fail -gt 0) { exit 1 } else { exit 0 }
