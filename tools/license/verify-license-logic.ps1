<#
    verify-license-logic.ps1 - probe logic cho LicenseService (finding C2-06).

    Nap DLL da build (Debug) va drive 5 ca de dam bao hanh vi bao mat duoc CHAY
    THAT, chu khong phai chi build Exit 0:
      1 valid-future     -> Valid
      2 past-expiry       -> Locked
      3 tampered          -> Locked
      4 missing/corrupt   -> Locked (KHONG nem)
      5 rollback-guard    -> Locked (moc dong ho vuot han chan ne-han)

    Chay SAU khi build Debug bang MSBuild duong dan day du. Thuan ASCII.
#>
#requires -Version 5.1
param(
    [string]$Dll,
    [string]$ConfigPath,
    [string]$Issuer
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
if (-not $Dll)        { $Dll        = Join-Path $scriptDir '..\..\TotalParking\bin\TotalParking.dll' }
if (-not $ConfigPath) { $ConfigPath = Join-Path $scriptDir '..\..\TotalParking\Web.config' }
if (-not $Issuer)     { $Issuer     = Join-Path $scriptDir 'issue-license.ps1' }

$work = Join-Path $env:TEMP ("lic-probe-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $work | Out-Null

function Cleanup { if (Test-Path $work) { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue } }

try {
    if (-not (Test-Path $Dll)) { throw ("Chua build DLL: " + $Dll + " (build Debug truoc)") }

    $txt = [System.IO.File]::ReadAllText($ConfigPath)
    $pubB64 = ([regex]::Match($txt, '<add key="license:publicKey"\s+value="([^"]*)"')).Groups[1].Value
    if (-not $pubB64 -or $pubB64 -eq 'PASTE_BASE64_PUBLIC_KEY') { throw "Chua co license:publicKey trong Web.config" }

    Add-Type -Path $Dll

    $lic = Join-Path $work 'license.lic'
    $hw  = Join-Path $work 'clock-highwater.dat'
    $enc = New-Object System.Text.UTF8Encoding($false)
    $today = [DateTime]::UtcNow

    function New-Svc { New-Object TotalParking.Services.License.LicenseService($pubB64, $work) }
    function Reset-Work { Get-ChildItem $work -File | Remove-Item -Force }
    function Issue([string]$expiry) {
        & powershell -NoProfile -ExecutionPolicy Bypass -File $Issuer -Issue -Expiry $expiry -Out $lic | Out-Null
        if ($LASTEXITCODE -ne 0) { throw ("Issue that bai: " + $expiry) }
    }
    function State-Of { param($s) "$($s.State)" }

    $pass = 0; $total = 5

    # 1) valid-future
    Reset-Work; Issue $today.AddDays(30).ToString('yyyy-MM-dd')
    if ((State-Of (New-Svc).GetStatus()) -eq 'Valid') { $pass++; Write-Host "1 valid-future : PASS" } else { Write-Host "1 valid-future : FAIL" }

    # 2) past-expiry
    Reset-Work; Issue $today.AddDays(-1).ToString('yyyy-MM-dd')
    if ((State-Of (New-Svc).GetStatus()) -eq 'Locked') { $pass++; Write-Host "2 past-expiry  : PASS" } else { Write-Host "2 past-expiry  : FAIL" }

    # 3) tampered
    Reset-Work; Issue $today.AddDays(30).ToString('yyyy-MM-dd')
    $lines = [System.IO.File]::ReadAllLines($lic)
    $lines[0] = $lines[0] -replace '"customerId":"L', '"customerId":"X'
    [System.IO.File]::WriteAllLines($lic, $lines, $enc)
    if ((State-Of (New-Svc).GetStatus()) -eq 'Locked') { $pass++; Write-Host "3 tampered     : PASS" } else { Write-Host "3 tampered     : FAIL" }

    # 4) missing/corrupt -> Locked, khong nem
    Reset-Work
    try {
        if ((State-Of (New-Svc).GetStatus()) -eq 'Locked') { $pass++; Write-Host "4 missing      : PASS" } else { Write-Host "4 missing      : FAIL" }
    } catch { Write-Host ("4 missing      : FAIL (nem: " + $_.Exception.Message + ")") }

    # 5) rollback-guard: license con han, nhung moc dong ho da vuot han
    Reset-Work; Issue $today.AddDays(10).ToString('yyyy-MM-dd')
    [System.IO.File]::WriteAllText($hw, $today.AddDays(20).ToString('yyyy-MM-ddTHH:mm:ssZ'), $enc)
    if ((State-Of (New-Svc).GetStatus()) -eq 'Locked') { $pass++; Write-Host "5 rollback-guard: PASS" } else { Write-Host "5 rollback-guard: FAIL" }

    Write-Host ("{0}/{1} cases PASS" -f $pass, $total)
    Cleanup
    if ($pass -eq $total) { exit 0 } else { exit 1 }
}
catch {
    Write-Host ("PROBE ERROR: " + $_.Exception.Message)
    Cleanup
    exit 1
}
