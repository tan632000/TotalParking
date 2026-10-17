# Kiem chung dem luot chay dong co (specs/d3-loi-va-dem-dong-co/task-02-dem-dong-co.md).
#
# Moi ca in PASS/FAIL. Thoat 0 chi khi DEM DU so ca PASS va khong ca nao FAIL.
#   bang_bit   bang bit trong DLL khop bang chep tay tu docs/NOTE FINAL.xlsx
#              (ban 13/10/2026 09:40) — hai lan chep doc lap phai trung nhau.
#   quy_tac    cac chuoi doc CIO gia tren DongCoDetector + kho GIA trong bo nho.
#   live_cio   theo doi CIO 100..102 THAT cua moi PLC trong -GiayTheoDoi giay
#              (chi DOC). FAIL neu bat ky bit nao trong 100.02..102.07 bat ma
#              khong co trong bang cua loai khoi, hoac bit dong co (tru
#              RAO_CHAN) bat trong khi khoi do chua tung co nguon + chieu.
#              RAO_CHAN bat luc dung yen la trang thai giu hop le. UNVERIFIED
#              (thoat != 0) neu ca cua so khong thay dong co nao hoat dong.
#   live_bang  motor_run co it nhat 1 dong; khong co thi NO_DATA (thoat != 0).
#
# Chay tu thu muc goc repo, SAU khi build:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_dong_co.ps1 [-GiayTheoDoi 300]

param([int]$GiayTheoDoi = 300)

$ErrorActionPreference = 'Stop'

$bin = Resolve-Path (Join-Path $PSScriptRoot '..\TotalParking\bin')
$dll = Join-Path $bin 'TotalParking.dll'
[void][Reflection.Assembly]::LoadFrom($dll)

Add-Type -ReferencedAssemblies $dll -TypeDefinition @'
using System;
using System.Collections.Generic;

public class KhoDongCoGia : TotalParking.Services.Plc.IMotorRunStore
{
    public List<string> Dong = new List<string>();
    public List<long> ThoiLuong = new List<long>();
    public List<DateTime> BatDau = new List<DateTime>();
    public List<DateTime> KetThuc = new List<DateTime>();

    public void Ghi(int blockNo, string motorCode, DateTime batDau, DateTime ketThuc, long thoiLuongMs)
    {
        Dong.Add(blockNo + ":" + motorCode);
        ThoiLuong.Add(thoiLuongMs);
        BatDau.Add(batDau);
        KetThuc.Add(ketThuc);
    }
}
'@

$script:failed = 0
$script:passed = 0
$SoCa = 4
$Det = [TotalParking.Services.Plc.DongCoDetector]

function Ket-Qua([string]$ten, [bool]$ok, [string]$chiTiet) {
    if ($ok) { Write-Output ("PASS  {0,-12} {1}" -f $ten, $chiTiet); $script:passed++ }
    else     { Write-Output ("FAIL  {0,-12} {1}" -f $ten, $chiTiet); $script:failed++ }
}

# ---------------------------------------------------------------- bang_bit
# Chep tay tu NOTE FINAL, doc lap voi DongCoDetector.BangBit. "word.bit=ma".
$mong = @{
    3  = @{ Nguon = '101.01'; DongCo = '100.04=201 100.05=202 100.06=102 100.07=RAO_CHAN 101.00=NAM_CHAM' }
    5  = @{ Nguon = '101.03'; DongCo = '100.04=201 100.05=202 100.06=203 100.07=102 101.00=103 101.01=RAO_CHAN 101.02=NAM_CHAM' }
    6  = @{ Nguon = '101.06'; DongCo = '100.04=201 100.05=202 100.06=102 100.07=RAO_CHAN 101.02=211 101.03=212 101.04=112 101.05=NAM_CHAM' }
    10 = @{ Nguon = '102.02'; DongCo = '100.04=201 100.05=202 100.06=203 100.07=102 101.00=103 101.01=RAO_CHAN 101.04=211 101.05=212 101.06=213 101.07=112 102.00=113 102.01=NAM_CHAM' }
}
function Ten-Bit($b) { return ('{0}.{1:00}' -f $b.Word, $b.So) }
$lech = @()
$bang = $Det::BangBit
if ($bang.Count -ne 4) { $lech += "so loai khoi = $($bang.Count)" }
foreach ($loai in $mong.Keys) {
    if (-not $bang.ContainsKey($loai)) { $lech += "thieu loai $loai"; continue }
    $l = $bang[$loai]
    if ((Ten-Bit $l.Nguon) -ne $mong[$loai].Nguon) { $lech += "loai $loai nguon $(Ten-Bit $l.Nguon)" }
    $thuc = (@($l.DongCo | ForEach-Object { (Ten-Bit $_.Bit) + '=' + $_.Ma }) -join ' ')
    if ($thuc -ne $mong[$loai].DongCo) { $lech += "loai $loai dong co: $thuc" }
}
if ((Ten-Bit $Det::Thuan) -ne '100.02' -or (Ten-Bit $Det::Nghich) -ne '100.03') { $lech += 'thuan/nghich' }
Ket-Qua 'bang_bit' ($lech.Count -eq 0) ("loai=[{0}] dong_co={1}{2}" -f (($bang.Keys | Sort-Object) -join ','), (($bang.Values | ForEach-Object { $_.DongCo.Count }) -join '/'), $(if ($lech) { ' LECH: ' + ($lech -join '; ') } else { '' }))

# ---------------------------------------------------------------- quy_tac
# Khoi 1 = loai 5: nguon 101.03, thuan 100.02, nghich 100.03, dong co 201 = 100.04.
$F = [Diagnostics.Stopwatch]::Frequency
function Cio([string[]]$bits) {
    $w = [uint16[]]@(0, 0, 0)
    foreach ($b in $bits) { $p = $b.Split('.'); $i = [int]$p[0] - 100; $w[$i] = $w[$i] -bor (1 -shl [int]$p[1]) }
    return ,$w
}
$OFF   = Cio @()
$CHAY  = Cio @('101.03', '100.02', '100.04')        # nguon + thuan + 201
$CHAYN = Cio @('101.03', '100.03', '100.04')        # nguon + nghich + 201
$KHONG_NGUON = Cio @('100.02', '100.04')            # 201 bat, thieu nguon
$CHI_201 = Cio @('100.04')                          # nguon va chieu da tat, 201 con bat
$goc = Get-Date '2026-10-13 08:00:00'

function Moi-Det { $k = New-Object KhoDongCoGia; $m = New-Object 'System.Collections.Generic.Dictionary[int,int]'; $m[1] = 5; $m[7] = 7; return @($k, (New-Object TotalParking.Services.Plc.DongCoDetector($k, $m))) }
function Doc($det, [double]$giay, $cio) { $det.QuanSat(1, $cio, $goc.AddSeconds($giay), [long]($giay * $F)) }

$buoc = @()
# AC-05: thay OFF, chay tu t=10 toi t=25, nguon/chieu tat truoc ma 201 con bat toi t=25.
$k, $d = Moi-Det
Doc $d 0 $OFF; Doc $d 10 $CHAY; Doc $d 15 $CHI_201; Doc $d 25 $OFF
$buoc += ,@('AC-05 mot luot', ($k.Dong.Count -eq 1 -and $k.Dong[0] -eq '1:201' -and $k.BatDau[0] -eq $goc.AddSeconds(10) -and $k.KetThuc[0] -eq $goc.AddSeconds(25) -and [math]::Abs($k.ThoiLuong[0] - 15000) -le 1))
# AC-06a: 201 bat khi thieu nguon roi tat -> khong luot.
$k, $d = Moi-Det
Doc $d 0 $OFF; Doc $d 1 $KHONG_NGUON; Doc $d 2 $OFF
$buoc += ,@('AC-06 thieu nguon', ($k.Dong.Count -eq 0))
# AC-06b: bat khi thieu nguon, du dieu kien tai t=5 (nghich) -> luot bat dau t=5.
$k, $d = Moi-Det
Doc $d 0 $OFF; Doc $d 1 $KHONG_NGUON; Doc $d 5 $CHAYN; Doc $d 9 $OFF
$buoc += ,@('AC-06 bat dau khi du', ($k.Dong.Count -eq 1 -and $k.BatDau[0] -eq $goc.AddSeconds(5) -and [math]::Abs($k.ThoiLuong[0] - 4000) -le 1))
# Khoi dong giua luot: lan doc dau da thay 201 chay -> khong ghi.
$k, $d = Moi-Det
Doc $d 0 $CHAY; Doc $d 1 $CHAY; Doc $d 2 $OFF
$buoc += ,@('khoi dong giua luot', ($k.Dong.Count -eq 0))
# Mat doc qua 3 nhip giua luot -> bo luot; luot sau van dem.
$k, $d = Moi-Det
Doc $d 0 $OFF; Doc $d 1 $CHAY; $d.DocLoi(1); $d.DocLoi(1); $d.DocLoi(1); $d.DocLoi(1); Doc $d 9 $OFF
$boLuot = ($k.Dong.Count -eq 0)
Doc $d 10 $CHAY; Doc $d 12 $OFF
$buoc += ,@('mat doc > 3 nhip', ($boLuot -and $k.Dong.Count -eq 1 -and [math]::Abs($k.ThoiLuong[0] - 2000) -le 1))
# Mat doc dung 3 nhip -> van giu luot.
$k, $d = Moi-Det
Doc $d 0 $OFF; Doc $d 1 $CHAY; $d.DocLoi(1); $d.DocLoi(1); $d.DocLoi(1); Doc $d 5 $OFF
$buoc += ,@('mat doc 3 nhip giu luot', ($k.Dong.Count -eq 1 -and [math]::Abs($k.ThoiLuong[0] - 4000) -le 1))
# Nhip dai 3 giay vi PLC khac cham, khoi van doc duoc moi nhip -> giu luot.
$k, $d = Moi-Det
Doc $d 0 $OFF; Doc $d 1 $CHAY; Doc $d 4 $CHAY; Doc $d 7 $CHAY; Doc $d 10 $OFF
$buoc += ,@('nhip dai giu luot', ($k.Dong.Count -eq 1 -and [math]::Abs($k.ThoiLuong[0] - 9000) -le 1))
# Khoi loai la (slot 7) bi bo qua, khong nem loi.
$k, $d = Moi-Det
$d.QuanSat(7, $OFF, $goc, 0); $d.QuanSat(7, $CHAY, $goc.AddSeconds(1), $F); $d.QuanSat(7, $OFF, $goc.AddSeconds(2), 2 * $F)
$buoc += ,@('khoi loai la', ($k.Dong.Count -eq 0))
$sai = @($buoc | Where-Object { -not $_[1] } | ForEach-Object { $_[0] })
Ket-Qua 'quy_tac' ($sai.Count -eq 0) ("{0}/{1} chuoi dung{2}" -f ($buoc.Count - $sai.Count), $buoc.Count, $(if ($sai) { ' SAI: ' + ($sai -join '; ') } else { '' }))

# ---------------------------------------------------------------- tien ich DB
function Chuoi-Ket-Noi {
    [xml]$x = Get-Content -LiteralPath 'C:\Users\Admin\Documents\Web\totalParking\Web.config' -Raw -Encoding UTF8
    return ($x.configuration.connectionStrings.add | Where-Object { $_.name -eq 'TotalParkingDb' }).connectionString
}
# mysql.exe chu khong MySqlConnector (ngoai IIS DLL do khong nap duoc). Mat khau
# di qua MYSQL_PWD, khong nam tren dong lenh.
function Sql-Danh-Sach([string]$sql) {
    $tham = @{}
    foreach ($phan in (Chuoi-Ket-Noi).Split(';')) { $kv = $phan.Split('=', 2); if ($kv.Count -eq 2) { $tham[$kv[0].Trim().ToLowerInvariant()] = $kv[1].Trim() } }
    $h = @($tham['server'], $tham['host'], $tham['data source']) | Where-Object { $_ } | Select-Object -First 1
    $u = @($tham['user id'], $tham['uid'], $tham['user'], $tham['username']) | Where-Object { $_ } | Select-Object -First 1
    $env:MYSQL_PWD = @($tham['password'], $tham['pwd']) | Where-Object { $_ } | Select-Object -First 1
    try {
        $out = & 'C:\Program Files\MySQL\MySQL Server 8.4\bin\mysql.exe' -h $h -u $u -N -B --default-character-set=utf8mb4 $tham['database'] -e $sql 2>&1
        if ($LASTEXITCODE -ne 0) { throw ("mysql.exe: " + ($out -join ' ')) }
    } finally { Remove-Item Env:\MYSQL_PWD -ErrorAction SilentlyContinue }
    $ra = @(); foreach ($dong in @($out)) { if ($dong) { $ra += ,@(([string]$dong).Split("`t")) } }
    return ,$ra
}

# ---------------------------------------------------------------- live_cio
try {
    $plcs = Sql-Danh-Sach "SELECT b.block_no, d.ip_address, b.slot_count FROM plc_device d JOIN block b USING(block_id) WHERE d.is_active = 1 AND b.is_active = 1 AND b.kind = 'Mechanical' ORDER BY b.block_no"
    $CIO = [TotalParking.Services.Plc.PlcMemoryArea]::CIO
    $kn = @()
    foreach ($p in $plcs) {
        $c = New-Object TotalParking.Services.Plc.OmronFinsUdpClient
        try { [void]$c.ConnectAsync($p[1], 9600, 1500).GetAwaiter().GetResult(); $kn += ,@([int]$p[0], [int]$p[2], $c) } catch { $c.Dispose() }
    }
    # Bit duoc phep theo loai: nguon, thuan, nghich, cac dong co. 100.00/100.01
    # nam ngoai dai kiem (100.02..102.07).
    $phep = @{}
    foreach ($loai in $bang.Keys) {
        $s = New-Object 'System.Collections.Generic.HashSet[string]'
        [void]$s.Add((Ten-Bit $bang[$loai].Nguon)); [void]$s.Add('100.02'); [void]$s.Add('100.03')
        foreach ($dc in $bang[$loai].DongCo) { [void]$s.Add((Ten-Bit $dc.Bit)) }
        $phep[$loai] = $s
    }
    $bitLa = @{}; $dcKhongDk = @{}; $dcBat = @{}; $coDk = @{}; $soLuot = 0
    $het = (Get-Date).AddSeconds($GiayTheoDoi)
    while ((Get-Date) -lt $het) {
        foreach ($x in $kn) {
            $khoi = $x[0]; $loai = $x[1]
            if (-not $bang.ContainsKey($loai)) { continue }
            try { $w = $x[2].ReadWordsAsync($CIO, 100, 3, 800).GetAwaiter().GetResult() } catch { continue }
            $soLuot++
            $on = @()
            for ($i = 0; $i -lt 3; $i++) { for ($b = 0; $b -lt 16; $b++) { if (($w[$i] -shr $b) -band 1) { $on += ('{0}.{1:00}' -f (100 + $i), $b) } } }
            $l = $bang[$loai]
            $nguon = $on -contains (Ten-Bit $l.Nguon); $chieu = ($on -contains '100.02') -or ($on -contains '100.03')
            if ($nguon -and $chieu) { $coDk[$khoi] = $true }
            foreach ($b in $on) {
                if ($b -lt '100.02' -or $b -gt '102.07') { continue }
                if (-not $phep[$loai].Contains($b)) { $bitLa["$khoi $b"] = $true }
            }
            foreach ($dc in $l.DongCo) {
                if ($on -contains (Ten-Bit $dc.Bit)) {
                    # RAO_CHAN bat luc dung yen la trang thai giu (nguoi dung chot
                    # 13/10/2026): khong phai vi pham, cung khong phai bang chung
                    # dong co hoat dong — chi tinh khi co nguon + chieu.
                    if ($dc.Ma -eq 'RAO_CHAN') {
                        if ($nguon -and $chieu) { $dcBat["$khoi $($dc.Ma)"] = $true }
                        continue
                    }
                    $dcBat["$khoi $($dc.Ma)"] = $true
                    if ($nguon -and $chieu) { $dcKhongDk.Remove("$khoi $($dc.Ma)") }
                    elseif (-not $coDk.ContainsKey($khoi)) { $dcKhongDk["$khoi $($dc.Ma)"] = $true }
                }
            }
        }
        Start-Sleep -Milliseconds 200
    }
    foreach ($x in $kn) { $x[2].Dispose() }
    # Dong co bat khi khoi chua tung co nguon+chieu o BAT KY lan doc nao trong cua so.
    $viPham = @($dcKhongDk.Keys | Where-Object { -not $coDk.ContainsKey([int]($_.Split(' ')[0])) })
    $chiTiet = ("khoi_doc_duoc={0} luot_doc={1} dong_co_tung_bat={2} khoi_tung_du_dk={3} bit_la={4} dc_bat_khong_dk={5}" -f `
        $kn.Count, $soLuot, $dcBat.Count, $coDk.Count, $bitLa.Count, $viPham.Count)
    if ($bitLa.Count) { $chiTiet += ' | bit la: ' + ((@($bitLa.Keys) | Sort-Object | Select-Object -First 15) -join ', ') }
    if ($viPham.Count) { $chiTiet += ' | dc bat khong dk: ' + (($viPham | Sort-Object | Select-Object -First 15) -join ', ') }
    if ($dcBat.Count -eq 0) {
        Write-Output ("UNVERIFIED live_cio: khong thay bit dong co nao bat trong {0}s. {1}" -f $GiayTheoDoi, $chiTiet); $script:failed++
    } else {
        Ket-Qua 'live_cio' ($bitLa.Count -eq 0 -and $viPham.Count -eq 0) $chiTiet
    }
} catch { Ket-Qua 'live_cio' $false ("loi: " + $_.Exception.Message) }

# ---------------------------------------------------------------- live_bang
try {
    $n = [int](Sql-Danh-Sach "SELECT COUNT(*) FROM motor_run")[0][0]
    if ($n -eq 0) { Write-Output 'NO_DATA     live_bang: motor_run chua co dong nao'; $script:failed++ }
    else {
        $moi = Sql-Danh-Sach "SELECT block_no, motor_code, started_at, ended_at, duration_ms FROM motor_run ORDER BY run_id DESC LIMIT 3"
        Ket-Qua 'live_bang' $true ("so_dong={0} moi_nhat: {1}" -f $n, (($moi | ForEach-Object { $_ -join ' ' }) -join ' | '))
    }
} catch { Ket-Qua 'live_bang' $false ("loi: " + $_.Exception.Message) }

Write-Output ''
if ($script:failed -eq 0 -and $script:passed -eq $SoCa) { Write-Output ("TONG KET: {0}/{1} PASS" -f $script:passed, $SoCa); exit 0 }
Write-Output ("TONG KET: {0} PASS, {1} FAIL / {2} ca" -f $script:passed, $script:failed, $SoCa); exit 1
