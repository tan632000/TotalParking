# Kiem chung trang Bao tri (specs/d3-loi-va-dem-dong-co/task-03-hien-thi-dong-co.md).
#
# Moi ca in PASS/FAIL. Thoat 0 chi khi DEM DU so ca PASS va khong ca nao FAIL.
# Goi API cua BAN DEPLOY (http://localhost:8080) roi so voi MySQL — chi DOC.
#   loi_theo_khoi      /BaoTri/LoiTheoKhoi khop so canh bao dang mo theo khoi
#   loi_mot_khoi       /BaoTri/Loi: dang_mo va lich_su khop DB, limit, thu tu
#   dong_co            /BaoTri/DongCo: du dong co cua loai khoi; nhanh co/chua co bang
#   thoi_gian          loai dong co + thoi gian = so lan x 20/24/12 s; nam cham khong co thoi gian
#   dau_vao_sai        block=abc -> 400; khoi do nen 901 -> 400
#   trang              /Home/Maintenance 200, goi du 3 endpoint, khong con du lieu bia
#
# Chay tu thu muc goc repo, SAU khi build va deploy:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_bao_tri.ps1

$ErrorActionPreference = 'Stop'
$Goc = 'http://localhost:8080'

$bin = Resolve-Path (Join-Path $PSScriptRoot '..\TotalParking\bin')
[void][Reflection.Assembly]::LoadFrom((Join-Path $bin 'TotalParking.dll'))

$script:failed = 0
$script:passed = 0
$SoCa = 6

function Ket-Qua([string]$ten, [bool]$ok, [string]$chiTiet) {
    if ($ok) { Write-Output ("PASS  {0,-15} {1}" -f $ten, $chiTiet); $script:passed++ }
    else     { Write-Output ("FAIL  {0,-15} {1}" -f $ten, $chiTiet); $script:failed++ }
}

# Tra ve @{ Ma = <status>; Json = <obj>; Than = <chuoi> }. Doc byte roi giai UTF-8:
# server tra "application/json" khong kem charset, de PowerShell tu giai thi hong dau.
function Goi([string]$duongDan) {
    $req = [Net.HttpWebRequest]::Create($Goc + $duongDan)
    $req.Timeout = 30000
    try { $res = $req.GetResponse() } catch [Net.WebException] { $res = $_.Exception.Response; if (-not $res) { throw } }
    try {
        $ms = New-Object IO.MemoryStream; $res.GetResponseStream().CopyTo($ms)
        $than = [Text.Encoding]::UTF8.GetString($ms.ToArray())
        $json = $null; try { $json = $than | ConvertFrom-Json } catch { }
        return @{ Ma = [int]$res.StatusCode; Json = $json; Than = $than }
    } finally { $res.Close() }
}

function Chuoi-Ket-Noi {
    [xml]$x = Get-Content -LiteralPath 'C:\Users\Admin\Documents\Web\totalParking\Web.config' -Raw -Encoding UTF8
    return ($x.configuration.connectionStrings.add | Where-Object { $_.name -eq 'TotalParkingDb' }).connectionString
}
# mysql.exe chu khong MySqlConnector (ngoai IIS DLL do khong nap duoc). Mat khau
# di qua MYSQL_PWD, khong nam tren dong lenh.
function Sql([string]$sql) {
    $tham = @{}
    foreach ($phan in (Chuoi-Ket-Noi).Split(';')) { $kv = $phan.Split('=', 2); if ($kv.Count -eq 2) { $tham[$kv[0].Trim().ToLowerInvariant()] = $kv[1].Trim() } }
    $h = @($tham['server'], $tham['host'], $tham['data source']) | Where-Object { $_ } | Select-Object -First 1
    $u = @($tham['user id'], $tham['uid'], $tham['user'], $tham['username']) | Where-Object { $_ } | Select-Object -First 1
    $env:MYSQL_PWD = @($tham['password'], $tham['pwd']) | Where-Object { $_ } | Select-Object -First 1
    try {
        $out = & (Join-Path ${env:ProgramFiles} 'MySQL\MySQL Server 8.4\bin\mysql.exe') -h $h -u $u -N -B --default-character-set=utf8mb4 $tham['database'] -e $sql 2>&1
        if ($LASTEXITCODE -ne 0) { throw ("mysql.exe: " + ($out -join ' ')) }
    } finally { $env:MYSQL_PWD = '' }
    $ra = @(); foreach ($dong in @($out)) { if ($dong) { $ra += ,@(([string]$dong).Split("`t")) } }
    return ,$ra
}

# ---------------------------------------------------------------- loi_theo_khoi
try {
    $lech = $null
    for ($lan = 0; $lan -lt 2; $lan++) {
        $api = Goi '/BaoTri/LoiTheoKhoi'
        $db  = Sql "SELECT block_no, COUNT(*) FROM canh_bao WHERE het_luc IS NULL AND block_no IS NOT NULL GROUP BY block_no"
        $mapDb = @{}; foreach ($h in $db) { $mapDb[[int]$h[0]] = [int]$h[1] }
        $mapApi = @{}; foreach ($x in @($api.Json.items)) { $mapApi[[int]$x.block_no] = [int]$x.dang_mo }
        $lech = @($mapDb.Keys + $mapApi.Keys | Sort-Object -Unique | Where-Object { $mapDb[$_] -ne $mapApi[$_] })
        if ($api.Ma -eq 200 -and $lech.Count -eq 0) { break }
        Start-Sleep -Seconds 2   # canh bao vua mo/dong giua hai lan doc
    }
    $tong = ($mapApi.Values | Measure-Object -Sum).Sum
    Ket-Qua 'loi_theo_khoi' ($api.Ma -eq 200 -and $lech.Count -eq 0 -and $mapApi.Count -gt 0) ("http={0} khoi_co_loi={1} tong_dang_mo={2} lech={3}" -f $api.Ma, $mapApi.Count, $tong, ($lech -join ','))
} catch { Ket-Qua 'loi_theo_khoi' $false ("loi: " + $_.Exception.Message) }

# ---------------------------------------------------------------- loi_mot_khoi
try {
    $chon = Sql "SELECT c.block_no FROM canh_bao c JOIN block b ON b.block_no = c.block_no AND b.kind = 'Mechanical' AND b.is_active = 1 WHERE c.het_luc IS NULL GROUP BY c.block_no ORDER BY COUNT(*) DESC, c.block_no LIMIT 1"
    $k = [int]$chon[0][0]
    $sai = @()
    for ($lan = 0; $lan -lt 2; $lan++) {
        $sai = @()
        $a  = Goi "/BaoTri/Loi?block=$k"
        $mo = (Sql "SELECT canh_bao_id FROM canh_bao WHERE block_no = $k AND het_luc IS NULL ORDER BY canh_bao_id") | ForEach-Object { [long]$_[0] }
        $ls = (Sql "SELECT canh_bao_id FROM canh_bao WHERE block_no = $k ORDER BY xay_ra_luc DESC, canh_bao_id DESC LIMIT 20") | ForEach-Object { [long]$_[0] }
        $apiMo = @($a.Json.dang_mo | ForEach-Object { [long]$_.id } | Sort-Object)
        $apiLs = @($a.Json.lich_su | ForEach-Object { [long]$_.id })
        if ($a.Ma -ne 200) { $sai += "http $($a.Ma)" }
        if ((@($mo) -join ',') -ne ($apiMo -join ',')) { $sai += "dang_mo api=[$($apiMo -join ',')] db=[$(@($mo) -join ',')]" }
        if ((@($ls) -join ',') -ne ($apiLs -join ',')) { $sai += "lich_su lech thu tu/tap" }
        if ($apiLs.Count -gt 20) { $sai += "lich_su > 20" }
        if ($sai.Count -eq 0) { break }
        Start-Sleep -Seconds 2
    }
    $a5 = Goi "/BaoTri/Loi?block=$k&limit=5"
    if (@($a5.Json.lich_su).Count -gt 5) { $sai += "limit=5 tra $(@($a5.Json.lich_su).Count)" }
    Ket-Qua 'loi_mot_khoi' ($sai.Count -eq 0) ("khoi={0} dang_mo={1} lich_su={2} limit5={3}{4}" -f $k, @($a.Json.dang_mo).Count, @($a.Json.lich_su).Count, @($a5.Json.lich_su).Count, $(if ($sai) { ' SAI: ' + ($sai -join '; ') } else { '' }))
} catch { Ket-Qua 'loi_mot_khoi' $false ("loi: " + $_.Exception.Message) }

# ---------------------------------------------------------------- dong_co
try {
    $bang = [TotalParking.Services.Plc.DongCoDetector]::BangBit
    $coBang = [int](Sql "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'motor_run'")[0][0]
    $sai = @(); $chiTiet = @()
    foreach ($loai in 3, 5, 6, 10) {
        $k = [int](Sql "SELECT MIN(block_no) FROM block WHERE kind = 'Mechanical' AND is_active = 1 AND slot_count = $loai")[0][0]
        $d = Goi "/BaoTri/DongCo?block=$k"
        $soDc = @($d.Json.tong_hop).Count
        $maApi = (@($d.Json.tong_hop | ForEach-Object { $_.motor_code }) -join ',')
        $maBang = (@($bang[$loai].DongCo | ForEach-Object { $_.Ma }) -join ',')
        if ($d.Ma -ne 200) { $sai += "khoi $k http $($d.Ma)" }
        if ($maApi -ne $maBang) { $sai += "khoi $k dong co [$maApi] != [$maBang]" }
        if ($coBang -eq 0) {
            if ($d.Json.co_du_lieu -ne $false) { $sai += "khoi $k chua co bang ma co_du_lieu=$($d.Json.co_du_lieu)" }
            if (@($d.Json.tong_hop | Where-Object { $_.tong -ne 0 -or $_.hom_nay -ne 0 }).Count) { $sai += "khoi $k chua co bang ma so khac 0" }
        } else {
            if ($d.Json.co_du_lieu -ne $true) { $sai += "khoi $k co bang ma co_du_lieu=$($d.Json.co_du_lieu)" }
            # So tung dong co. Lay DB SAU API: lượt chạy kết thúc xen giữa chỉ làm
            # DB lớn hơn, nên API > DB mới là sai; API < DB thì gọi lại một lần.
            for ($lan = 0; $lan -lt 2; $lan++) {
                $dbMa = @{}; foreach ($h in (Sql "SELECT motor_code, COUNT(*), SUM(ended_at >= CURDATE()) FROM motor_run WHERE block_no = $k GROUP BY motor_code")) { $dbMa[$h[0]] = @([int]$h[1], [int]$h[2]) }
                $lechMa = @()
                foreach ($x in @($d.Json.tong_hop)) {
                    $db = $dbMa[$x.motor_code]; if (-not $db) { $db = @(0, 0) }
                    if ([int]$x.tong -ne $db[0] -or [int]$x.hom_nay -ne $db[1]) { $lechMa += "$($x.motor_code) api=$($x.tong)/$($x.hom_nay) db=$($db[0])/$($db[1])" }
                }
                if ($lechMa.Count -eq 0) { break }
                $d = Goi "/BaoTri/DongCo?block=$k"
            }
            if ($lechMa.Count) { $sai += "khoi $k lech: " + ($lechMa -join ', ') }
        }
        $chiTiet += "loai$loai=khoi$k/$soDc dc"
    }
    $nhanh = if ($coBang -eq 0) { 'nhanh=CHUA_CO_BANG' } else { 'nhanh=DA_CO_BANG' }
    Ket-Qua 'dong_co' ($sai.Count -eq 0) ("{0} {1}{2}" -f $nhanh, ($chiTiet -join ' '), $(if ($sai) { ' SAI: ' + ($sai -join '; ') } else { '' }))
} catch { Ket-Qua 'dong_co' $false ("loi: " + $_.Exception.Message) }

# ---------------------------------------------------------------- thoi_gian
# Thoi gian hoat dong = so lan x chuan cua loai (nguoi dung chot 16/10/2026).
# Bang loai CHEP DOC LAP o day, khong doc tu DLL: phan loai sai trong code thi
# hai bang lech nhau va ca nay FAIL.
try {
    $loaiMong = @{
        '102' = 'TRUOT'; '103' = 'TRUOT'; '112' = 'TRUOT'; '113' = 'TRUOT'
        '201' = 'NANG'; '202' = 'NANG'; '203' = 'NANG'; '211' = 'NANG'; '212' = 'NANG'; '213' = 'NANG'
        'RAO_CHAN' = 'HANG_RAO'; 'NAM_CHAM' = 'NAM_CHAM'
    }
    $chuan = @{ 'TRUOT' = 20; 'NANG' = 24; 'HANG_RAO' = 12; 'NAM_CHAM' = $null }
    $thuTu = @('TRUOT', 'NANG', 'HANG_RAO', 'NAM_CHAM')
    $sai = @(); $chiTiet = @()
    foreach ($loaiKhoi in 3, 5, 6, 10) {
        $k = [int](Sql "SELECT MIN(block_no) FROM block WHERE kind = 'Mechanical' AND is_active = 1 AND slot_count = $loaiKhoi")[0][0]
        $d = (Goi "/BaoTri/DongCo?block=$k").Json
        foreach ($x in @($d.tong_hop)) {
            $l = $loaiMong[$x.motor_code]
            if ($x.loai -ne $l) { $sai += "khoi $k $($x.motor_code) loai=$($x.loai) mong=$l"; continue }
            $c = $chuan[$l]
            if ($c -eq $null) {
                if ($x.thoi_gian_tong_giay -ne $null -or $x.thoi_gian_hom_nay_giay -ne $null -or $x.thoi_gian_chuan_giay -ne $null) { $sai += "khoi $k $($x.motor_code) nam cham co thoi gian" }
            } else {
                if ($x.thoi_gian_tong_giay -eq $null -or $x.thoi_gian_hom_nay_giay -eq $null) { $sai += "khoi $k $($x.motor_code) thieu thoi gian" }
                if ([int]$x.thoi_gian_chuan_giay -ne $c) { $sai += "khoi $k $($x.motor_code) chuan=$($x.thoi_gian_chuan_giay)" }
                if ([long]$x.thoi_gian_tong_giay -ne [long]$x.tong * $c) { $sai += "khoi $k $($x.motor_code) tong $($x.tong)x$c != $($x.thoi_gian_tong_giay)" }
                if ([long]$x.thoi_gian_hom_nay_giay -ne [long]$x.hom_nay * $c) { $sai += "khoi $k $($x.motor_code) hom_nay $($x.hom_nay)x$c != $($x.thoi_gian_hom_nay_giay)" }
            }
        }
        # theo_loai = cong cac dong co cung loai, dung thu tu, chi loai co mat.
        $coMat = @($thuTu | Where-Object { $tt = $_; @($d.tong_hop | Where-Object { $loaiMong[$_.motor_code] -eq $tt }).Count -gt 0 })
        $apiLoai = @($d.theo_loai | Where-Object { $_ } | ForEach-Object { $_.loai })
        if (($apiLoai -join ',') -ne ($coMat -join ',')) { $sai += "khoi $k theo_loai=[$($apiLoai -join ',')] mong=[$($coMat -join ',')]" }
        foreach ($tl in @($d.theo_loai | Where-Object { $_ })) {
            $cung = @($d.tong_hop | Where-Object { $_.loai -eq $tl.loai })
            $sTong = ($cung | ForEach-Object { [int]$_.tong } | Measure-Object -Sum).Sum
            $sNay  = ($cung | ForEach-Object { [int]$_.hom_nay } | Measure-Object -Sum).Sum
            $c = $chuan[$tl.loai]
            if ([int]$tl.so_lan_tong -ne $sTong -or [int]$tl.so_lan_hom_nay -ne $sNay -or [int]$tl.so_dong_co -ne $cung.Count) { $sai += "khoi $k theo_loai $($tl.loai) so lan lech" }
            if ($c -eq $null) { if ($tl.thoi_gian_tong_giay -ne $null -or $tl.thoi_gian_hom_nay_giay -ne $null) { $sai += "khoi $k theo_loai NAM_CHAM co thoi gian" } }
            elseif ([long]$tl.thoi_gian_tong_giay -ne [long]$sTong * $c -or [long]$tl.thoi_gian_hom_nay_giay -ne [long]$sNay * $c) { $sai += "khoi $k theo_loai $($tl.loai) thoi gian lech" }
        }
        $chiTiet += ("khoi{0}:" -f $k) + ((@($d.theo_loai) | ForEach-Object { "$($_.loai)=$($_.so_lan_tong)l/$($_.thoi_gian_tong_giay)s" }) -join ',')
    }
    # Khoi co luot chay that (neu co): ca nay chi co nghia khi con so khac 0.
    $coThat = Sql "SELECT block_no FROM motor_run GROUP BY block_no ORDER BY COUNT(*) DESC LIMIT 1"
    if ($coThat.Count) {
        $k = [int]$coThat[0][0]; $d = (Goi "/BaoTri/DongCo?block=$k").Json
        foreach ($x in @($d.tong_hop | Where-Object { $_.tong -gt 0 })) {
            $c = $chuan[$loaiMong[$x.motor_code]]
            if ($c -ne $null -and [long]$x.thoi_gian_tong_giay -ne [long]$x.tong * $c) { $sai += "khoi that $k $($x.motor_code) sai" }
        }
        $chiTiet += ("khoi_that{0}:" -f $k) + ((@($d.theo_loai) | ForEach-Object { "$($_.loai)=$($_.so_lan_tong)l/$($_.thoi_gian_tong_giay)s" }) -join ',')
    }
    # Khong co luot chay nao co chuan thi moi phep nhan o tren deu la 0 x c = 0:
    # ca nay khong kiem duoc he so, phai bao ra chu khong PASS rong.
    $coLuotCoChuan = [int](Sql "SELECT COUNT(*) FROM motor_run WHERE motor_code <> 'NAM_CHAM'")[0][0]
    if ($coLuotCoChuan -eq 0) { $sai += 'motor_run chua co luot chay truot/nang/hang rao: khong kiem duoc he so' }
    $t = Goi '/Home/Maintenance'
    foreach ($s in 'thoi_gian_tong_giay', 'theo_loai', 'ten_loai') { if ($t.Than -notlike "*$s*") { $sai += "trang thieu $s" } }
    Ket-Qua 'thoi_gian' ($sai.Count -eq 0) (($chiTiet -join ' ') + $(if ($sai) { ' SAI: ' + (($sai | Select-Object -First 10) -join '; ') } else { '' }))
} catch { Ket-Qua 'thoi_gian' $false ("loi: " + $_.Exception.Message) }

# ---------------------------------------------------------------- dau_vao_sai
try {
    $m1 = (Goi '/BaoTri/Loi?block=abc').Ma
    $m2 = (Goi '/BaoTri/DongCo?block=abc').Ma
    $m3 = (Goi '/BaoTri/DongCo?block=901').Ma
    $m4 = (Goi '/BaoTri/DongCo').Ma
    Ket-Qua 'dau_vao_sai' ($m1 -eq 400 -and $m2 -eq 400 -and $m3 -eq 400 -and $m4 -eq 400) ("Loi?abc={0} DongCo?abc={1} DongCo?901={2} DongCo?thieu={3}" -f $m1, $m2, $m3, $m4)
} catch { Ket-Qua 'dau_vao_sai' $false ("loi: " + $_.Exception.Message) }

# ---------------------------------------------------------------- trang
try {
    $t = Goi '/Home/Maintenance'
    # '/BaoTri/Loi"' kem dau nhay dong (Razor sinh ra trong chuoi URL_LOI): chi
    # '/BaoTri/Loi' thi luon khop vi nam san trong '/BaoTri/LoiTheoKhoi'.
    $thieu = @('/BaoTri/LoiTheoKhoi"', '/BaoTri/Loi"', '/BaoTri/DongCo"', '/Monitor/BlockMap"', 'btChonKhoi') | Where-Object { $t.Than -notlike "*$_*" }
    $bia = @('Math.random', 'Xuất Work Order', 'Zone A – Hầm B1', 'LIFT_MOTOR_04') | Where-Object { $t.Than -like "*$_*" }
    Ket-Qua 'trang' ($t.Ma -eq 200 -and $thieu.Count -eq 0 -and $bia.Count -eq 0) ("http={0} thieu=[{1}] con_bia=[{2}]" -f $t.Ma, ($thieu -join ','), ($bia -join ','))
} catch { Ket-Qua 'trang' $false ("loi: " + $_.Exception.Message) }

Write-Output ''
if ($script:failed -eq 0 -and $script:passed -eq $SoCa) { Write-Output ("TONG KET: {0}/{1} PASS" -f $script:passed, $SoCa); exit 0 }
Write-Output ("TONG KET: {0} PASS, {1} FAIL / {2} ca" -f $script:passed, $script:failed, $SoCa); exit 1
