# Kiem chung duong di man tai xe (specs/man-tai-xe-zones-map).
#
# -ChiDll : chi cac ca dll_* (task 02). Nap bin\TotalParking.dll, goi
#           DriverLaneMap.RouteToBlock QUA INSTANCE, khong can IIS.
# Khong co -ChiDll: them nguon_drawroute va 3 ca live_* (task 03), doi ban deploy
#           C:\Users\Admin\Documents\Web\totalParking da publish, site o :8080.
# Moi ca in PASS/FAIL. Thoat 0 chi khi DEM DU so ca PASS va khong ca nao FAIL.
#
# Chay tu thu muc goc repo, SAU khi build:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_duong_tai_xe.ps1 -ChiDll
param([switch]$ChiDll)

$ErrorActionPreference = 'Stop'

$goc  = Resolve-Path (Join-Path $PSScriptRoot '..')
$dll  = Join-Path $goc 'TotalParking\bin\TotalParking.dll'
$json = Join-Path $goc 'TotalParking\App_Data\driver_lanes.json'
# LoadFrom chu khong Add-Type -Path: Add-Type duyet moi kieu trong DLL web va co
# the vap phu thuoc MVC/MySqlConnector khong lien quan toi bai kiem.
[void][Reflection.Assembly]::LoadFrom($dll)

$script:ket = @()
function Ghi([string]$ten, [bool]$dat, [string]$chiTiet) {
    $nhan = if ($dat) { 'PASS' } else { 'FAIL' }
    Write-Output ('{0}  {1,-24} {2}' -f $nhan, $ten, $chiTiet)
    $script:ket += [pscustomobject]@{ Ten = $ten; Dat = $dat }
}

function MoiBanDo([string]$duongDan) {
    New-Object TotalParking.Services.DriverLaneMap -ArgumentList $duongDan
}

# Ca loi: Found = false, Reason khac rong, KHONG nem loi.
function KiemLoi([string]$ten, $banDo, [int]$blockNo) {
    try {
        $r = $banDo.RouteToBlock($blockNo)
        Ghi $ten ((-not $r.Found) -and -not [string]::IsNullOrEmpty($r.Reason)) ("found={0} reason={1}" -f $r.Found, $r.Reason)
    } catch {
        Ghi $ten $false ("nem loi: " + $_.Exception.GetType().Name + " " + $_.Exception.Message)
    }
}

# Ghi file KHONG BOM, giong file that do script sinh ghi.
function GhiFile([string]$duongDan, [string]$noiDung) {
    [IO.File]::WriteAllText($duongDan, $noiDung, (New-Object Text.UTF8Encoding $false))
}

$tam = Join-Path ([IO.Path]::GetTempPath()) ('kiem_chung_duong_tai_xe_' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tam | Out-Null
try {
    # Ky vong lay tu chinh file JSON bang ConvertFrom-Json, KHONG qua code C# dang bi kiem.
    $noiDungThat = [IO.File]::ReadAllText($json)
    $du = $noiDungThat | ConvertFrom-Json
    $nut = @{}
    foreach ($n in $du.nodes) { $nut[[int]$n.id] = $n }
    $entry = $nut[[int]$du.entry]
    # Tap canh theo TOA DO, ca hai chieu: de bat ban cai dat ve thang [entry, block]
    # (duong doan) ma van dung diem dau/diem cuoi.
    $canh = @{}
    foreach ($e in $du.edges) {
        $a = $nut[[int]$e[0]]; $b = $nut[[int]$e[1]]
        $canh["$($a.x),$($a.y)>$($b.x),$($b.y)"] = $true
        $canh["$($b.x),$($b.y)>$($a.x),$($a.y)"] = $true
    }

    # ---- dll_moi_block: moi block trong file (112 co khi + 901-906)
    $banDo = MoiBanDo $json
    $loi = @(); $soBlock = 0; $soNen = 0
    foreach ($p in $du.blocks.PSObject.Properties) {
        $soBlock++
        $no = [int]$p.Name; $b = $p.Value
        if ($no -ge 901) { $soNen++ }
        try { $r = $banDo.RouteToBlock($no) } catch { $loi += "${no}:nem_loi"; continue }
        if (-not $r.Found)          { $loi += "${no}:khong_tim_thay($($r.Reason))"; continue }
        $d = $r.Points
        if ($d.Count -lt 2)         { $loi += "${no}:it_diem"; continue }
        if ($d[0].X -ne $entry.x -or $d[0].Y -ne $entry.y) { $loi += "${no}:dau_khong_phai_entry"; continue }
        $c = $d[$d.Count - 1]
        if ($c.X -ne [int]$b.x -or $c.Y -ne [int]$b.y)     { $loi += "${no}:cuoi_khong_phai_block"; continue }
        $den = $nut[[int]$b.node]; $apChot = $d[$d.Count - 2]
        if ($apChot.X -ne $den.x -or $apChot.Y -ne $den.y) { $loi += "${no}:ap_chot_khong_phai_nut_den"; continue }
        $ngoaiLan = $false
        for ($i = 0; $i -lt $d.Count - 2; $i++) {
            if (-not $canh.ContainsKey("$($d[$i].X),$($d[$i].Y)>$($d[$i+1].X),$($d[$i+1].Y)")) { $ngoaiLan = $true; break }
        }
        if ($ngoaiLan) { $loi += "${no}:doan_khong_phai_canh"; continue }
        foreach ($q in $d) {
            if ($q.X -lt 0 -or $q.Y -lt 0 -or $q.X -ge 4800 -or $q.Y -ge 3584) { $loi += "${no}:ngoai_khung"; break }
        }
    }
    Ghi 'dll_moi_block' (($loi.Count -eq 0) -and $soBlock -eq 118 -and $soNen -eq 6) `
        ("{0} block (do nen {1}), entry=({2},{3}); loi: [{4}]" -f $soBlock, $soNen, $entry.x, $entry.y, ($loi -join ', '))

    # ---- dll_file_thieu
    KiemLoi 'dll_file_thieu' (MoiBanDo (Join-Path $tam 'khong_co.json')) 1

    # ---- dll_file_hong
    $f = Join-Path $tam 'hong.json'; GhiFile $f '{ "frame": { "w": 4800, '
    KiemLoi 'dll_file_hong' (MoiBanDo $f) 1

    # ---- dll_khung_lech: khung plan_map cu
    $f = Join-Path $tam 'khung.json'
    GhiFile $f ($noiDungThat -replace '"w":\s*4800', '"w":1594' -replace '"h":\s*3584', '"h":1300')
    KiemLoi 'dll_khung_lech' (MoiBanDo $f) 1

    # ---- dll_nut_treo: them mot canh tro toi id khong ton tai
    $f = Join-Path $tam 'treo.json'
    GhiFile $f ($noiDungThat -replace '"edges":\s*\[', ('"edges":[[' + $du.entry + ',999999],'))
    KiemLoi 'dll_nut_treo' (MoiBanDo $f) 1

    # ---- dll_block_la: block khong co trong file
    KiemLoi 'dll_block_la' $banDo 777

    # ---- dll_khong_cache_loi: cung MOT instance, file hong roi sua lai
    $f = Join-Path $tam 'sua.json'; GhiFile $f '{ hong'
    $mot = MoiBanDo $f
    $r1 = $mot.RouteToBlock(1)
    $mocHong = [IO.File]::GetLastWriteTimeUtc($f)
    GhiFile $f $noiDungThat
    # Tra mtime ve DUNG moc cua lan ghi hong: ban cai dat cache loi theo moc thoi
    # gian se tra lai loi cu va ca nay FAIL; chi ban khong cache loi moi PASS.
    [IO.File]::SetLastWriteTimeUtc($f, $mocHong)
    $r2 = $mot.RouteToBlock(1)
    Ghi 'dll_khong_cache_loi' ((-not $r1.Found) -and $r2.Found) ("lan1 found={0}; sau khi sua lan2 found={1} so_diem={2}" -f $r1.Found, $r2.Found, $r2.Points.Count)
}
finally {
    Remove-Item -Recurse -Force $tam
}

$soCa = 7
if (-not $ChiDll) {
    $soCa = 11
    $deploy = 'C:\Users\Admin\Documents\Web\totalParking'
    $site   = 'http://localhost:8080'

    # ---- nguon_drawroute: kich thuoc theo view_w, co ve block_no
    $view = [IO.File]::ReadAllText((Join-Path $goc 'TotalParking\Views\Home\DriverGuide.cshtml'))
    $m = [regex]::Match($view, '(?s)function drawRoute\(.*?(?=function render\()')
    # Bo chu thich truoc khi kiem: chu thich duoc phep nhac so cu, va khong duoc
    # dem nhu code that (vd "payload.block_no" chi nam trong mot dong chu thich).
    $code = [regex]::Replace($m.Value, '//[^\r\n]*', '')
    $soCung = @([regex]::Matches($code, '(?<![\w.])(14|40|12)(?![\w.])') | ForEach-Object { $_.Value })
    $coViewW = $code.Contains('payload.view_w'); $coSo = $code -match '\.textContent\s*=\s*payload\.block_no'
    # View dang chay phai dung la view vua kiem.
    $hView = (Get-FileHash (Join-Path $goc 'TotalParking\Views\Home\DriverGuide.cshtml')).Hash
    $fViewDeploy = Join-Path $deploy 'Views\Home\DriverGuide.cshtml'
    $trungDeploy = (Test-Path $fViewDeploy) -and ((Get-FileHash $fViewDeploy).Hash -eq $hView)
    Ghi 'nguon_drawroute' ($m.Success -and $soCung.Count -eq 0 -and $coViewW -and $coSo -and $trungDeploy) `
        ("tim_thay={0}; so_cung=[{1}]; view_w={2}; ve_block_no={3}; view_deploy_trung_nguon={4}" -f $m.Success, ($soCung -join ','), $coViewW, $coSo, $trungDeploy)

    # ---- live_file_deploy: file JSON co trong ban deploy va trung SHA-256 nguon
    $fDeploy = Join-Path $deploy 'App_Data\driver_lanes.json'
    if (Test-Path $fDeploy) {
        $h1 = (Get-FileHash $json).Hash; $h2 = (Get-FileHash $fDeploy).Hash
        Ghi 'live_file_deploy' ($h1 -eq $h2) ("nguon={0} deploy={1}" -f $h1.Substring(0,16), $h2.Substring(0,16))
    } else {
        Ghi 'live_file_deploy' $false "thieu $fDeploy"
    }

    # ---- live_khung_driverroute: phan hoi tra khung 4800x3584 (moi trang thai)
    try {
        $r = Invoke-WebRequest -UseBasicParsing -TimeoutSec 15 "$site/Monitor/DriverRoute"
        $p = $r.Content | ConvertFrom-Json
        Ghi 'live_khung_driverroute' ($p.view_w -eq 4800 -and $p.view_h -eq 3584) ("state={0} view={1}x{2} block={3} reason={4}" -f $p.state, $p.view_w, $p.view_h, $p.block_no, $p.reason)
    } catch {
        Ghi 'live_khung_driverroute' $false ("loi: " + $_.Exception.Message)
    }

    # ---- live_trang_driverguide: nen zones_map co chong cache, khong con plan_map
    try {
        $html = (Invoke-WebRequest -UseBasicParsing -TimeoutSec 30 "$site/Home/DriverGuide").Content
        $coZones = $html -match 'zones_map\.jpeg\?v=[1-9][0-9]*'
        $coPlan  = $html.Contains('plan_map.jpg')
        Ghi 'live_trang_driverguide' ($coZones -and -not $coPlan) ("zones_map?v={0}; plan_map.jpg={1}" -f $coZones, $coPlan)
    } catch {
        Ghi 'live_trang_driverguide' $false ("loi: " + $_.Exception.Message)
    }
}
$soDat = @($script:ket | Where-Object { $_.Dat }).Count
$soHong = @($script:ket | Where-Object { -not $_.Dat })
Write-Output ''
if ($soHong.Count -eq 0 -and $soDat -eq $soCa) {
    Write-Output ("TONG KET: {0}/{1} PASS" -f $soDat, $soCa)
    exit 0
}
Write-Output ("TONG KET: {0} PASS / can {1}; FAIL: {2}" -f $soDat, $soCa, (($soHong | ForEach-Object { $_.Ten }) -join ', '))
exit 1
