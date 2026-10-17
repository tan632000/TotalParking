# Kiem chung loi D3 -> canh_bao (specs/d3-loi-va-dem-dong-co/task-01-loi-d3.md).
#
# Moi ca in PASS/FAIL. Thoat 0 chi khi DEM DU so ca PASS va khong ca nao FAIL.
#   bang_ma, chuyen_trang_thai, khoi_dong, db_loi
#       chay tren LoiD3Service cua DLL vua build + kho canh bao GIA trong bo nho.
#   dong_kem_xac_nhan
#       kho THAT dong canh bao kem nguoi xac nhan tu dong (bat qua delegate).
#   live
#       doc D3 THAT cua moi PLC (chi DOC) roi so voi canh bao PLC_D3: dang mo
#       tren DB ma ban deploy dang ghi. Can ban deploy da chep DLL moi.
#
# Chay tu thu muc goc repo, SAU khi build:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_loi_d3.ps1

$ErrorActionPreference = 'Stop'

$bin = Resolve-Path (Join-Path $PSScriptRoot '..\TotalParking\bin')
$dll = Join-Path $bin 'TotalParking.dll'
[void][Reflection.Assembly]::LoadFrom($dll)

# ---------------------------------------------------------------- kho gia
Add-Type -ReferencedAssemblies $dll -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Linq;

public class KhoD3Gia : TotalParking.Services.Plc.ICanhBaoD3Store
{
    public HashSet<string> DangMo = new HashSet<string>();
    public List<string> NhatKy = new List<string>();
    public int SoMo, SoDong;
    public bool NemKhiMo;

    public IList<string> KhoaDangMo(int blockNo)
    {
        string tienTo = "PLC_D3:" + blockNo + ":";
        return DangMo.Where(k => k.StartsWith(tienTo)).ToList();
    }

    public void Mo(int blockNo, int? zoneId, int ma, string nhan, string khoa)
    {
        if (NemKhiMo) throw new Exception("DB gia loi khi mo");
        DangMo.Add(khoa);
        SoMo++;
        NhatKy.Add("MO " + khoa + " " + nhan);
    }

    public void Dong(string khoa)
    {
        DangMo.Remove(khoa);
        SoDong++;
        NhatKy.Add("DONG " + khoa);
    }
}
'@

# ---------------------------------------------------------------- tien ich
$script:failed = 0
$script:passed = 0
$SoCa = 6
$Svc = [TotalParking.Services.Plc.LoiD3Service]

function Ket-Qua([string]$ten, [bool]$ok, [string]$chiTiet) {
    if ($ok) { Write-Output ("PASS  {0,-20} {1}" -f $ten, $chiTiet); $script:passed++ }
    else     { Write-Output ("FAIL  {0,-20} {1}" -f $ten, $chiTiet); $script:failed++ }
}

function Moi([int]$nguong = 3) {
    $kho = New-Object KhoD3Gia
    $svc = New-Object TotalParking.Services.Plc.LoiD3Service($kho, $nguong)
    return @($kho, $svc)
}

# Day mot chuoi gia tri D3 vao mot khoi. 'x' = lan doc loi (khong goi QuanSat).
function Day($svc, [int]$khoi, $chuoi) {
    foreach ($v in $chuoi) {
        if ($v -ne 'x') { $svc.QuanSat($khoi, 4, [int]$v) }
    }
}

# ---------------------------------------------------------------- bang_ma
$bang = $Svc::BangMa
$maDung = @(1,2,3,5,6,7,9,10,11,12,14,17,18,20,22,74,75,79,80,84,85,102,103,110,114,115,119)
$thieu = @($maDung | Where-Object { -not $bang.ContainsKey($_) })
$thua  = @($bang.Keys | Where-Object { $maDung -notcontains $_ })
$ok = ($bang.Count -eq 27) -and ($thieu.Count -eq 0) -and ($thua.Count -eq 0) `
      -and ($bang[102] -eq 'Cảnh báo quá tải động cơ') `
      -and ($bang[1] -eq 'Cảnh báo nút dừng khẩn cấp') -and (-not $bang.ContainsKey(899))
Ket-Qua 'bang_ma' $ok ("so_ma={0} thieu=[{1}] thua=[{2}] 102='{3}' co_899={4}" -f $bang.Count, ($thieu -join ','), ($thua -join ','), $bang[102], $bang.ContainsKey(899))

# ---------------------------------------------------------------- chuyen_trang_thai
$kho, $svc = Moi
$buoc = @()
Day $svc 7 @(0,102,102,102);   $buoc += ,@('mo 102',        ($kho.SoMo -eq 1 -and $kho.DangMo.Contains('PLC_D3:7:102')))
Day $svc 7 @('x',102);         $buoc += ,@('loi doc khong trung', ($kho.SoMo -eq 1 -and $kho.SoDong -eq 0))
Day $svc 7 @(3,3,3);           $buoc += ,@('102 -> 3',      ($kho.DangMo.Count -eq 1 -and $kho.DangMo.Contains('PLC_D3:7:3') -and $kho.SoDong -eq 1))
Day $svc 7 @(899,899,899);     $buoc += ,@('899 dong 3, khong mo', ($kho.DangMo.Count -eq 0 -and $kho.SoMo -eq 2 -and $kho.SoDong -eq 2))
$truocNhay = $kho.NhatKy.Count
Day $svc 7 @(102,0,102,0);     $buoc += ,@('nhap nhay khong gi', ($kho.NhatKy.Count -eq $truocNhay))
Day $svc 7 @(0,0);             $buoc += ,@('ve 0 khong gi', ($kho.DangMo.Count -eq 0 -and $kho.NhatKy.Count -eq $truocNhay))
# Khoi khac khong bi dung: khoi 2 mo 1 roi khoi 21 ve 0 khong duoc dong khoa cua khoi 2.
Day $svc 2 @(1,1,1); Day $svc 21 @(0,0,0)
$buoc += ,@('khoa 2 khong lan 21', ($kho.DangMo.Contains('PLC_D3:2:1')))
$sai = @($buoc | Where-Object { -not $_[1] } | ForEach-Object { $_[0] })
Ket-Qua 'chuyen_trang_thai' ($sai.Count -eq 0) ("{0}/{1} buoc dung{2}; nhat_ky: {3}" -f ($buoc.Count - $sai.Count), $buoc.Count, $(if ($sai) { ' SAI: ' + ($sai -join '; ') } else { '' }), ($kho.NhatKy -join ' | '))

# ---------------------------------------------------------------- khoi_dong
# Ung dung vua khoi dong, DB con khoa mo tu truoc; lan doc dau loi, cac lan
# sau doc ra dung ma do -> khong dong, khong mo trung.
$kho, $svc = Moi
[void]$kho.DangMo.Add('PLC_D3:9:102')
Day $svc 9 @('x','x')
$giu = $kho.DangMo.Contains('PLC_D3:9:102') -and $kho.SoDong -eq 0
Day $svc 9 @(102,102,102)
$ok = $giu -and $kho.DangMo.Count -eq 1 -and $kho.SoMo -eq 0 -and $kho.SoDong -eq 0
# Lan khoi dong thu hai: khoa mo nhung PLC da het loi -> dong sau 3 lan doc 0.
$kho2, $svc2 = Moi
[void]$kho2.DangMo.Add('PLC_D3:9:102')
Day $svc2 9 @(0,0,0)
$ok = $ok -and $kho2.DangMo.Count -eq 0 -and $kho2.SoDong -eq 1
Ket-Qua 'khoi_dong' $ok ("giu_khi_doc_loi={0} khong_mo_trung={1} dong_khi_het_loi={2}" -f $giu, ($kho.SoMo -eq 0), ($kho2.SoDong -eq 1))

# ---------------------------------------------------------------- db_loi
# DB nem loi luc mo -> khong duoc coi la da hoa giai; lan doc on dinh ke tiep
# mo lai, va chi mo DUNG mot dong.
$kho, $svc = Moi
$kho.NemKhiMo = $true
Day $svc 5 @(102,102,102)
$truoc = $kho.DangMo.Count
$kho.NemKhiMo = $false
Day $svc 5 @(102)
Day $svc 5 @(102)
$ok = ($truoc -eq 0) -and $kho.DangMo.Contains('PLC_D3:5:102') -and $kho.SoMo -eq 1
Ket-Qua 'db_loi' $ok ("sau_loi={0} sau_hoi_phuc={1} so_lan_mo={2}" -f $truoc, $kho.DangMo.Count, $kho.SoMo)

# ---------------------------------------------------------------- dong_kem_xac_nhan
# Kho THAT (CanhBaoD3Store) phai dong kem nguoi xac nhan tu dong. Kho gia o
# tren khong thay duoc dieu nay; thieu no thi dong da dong van hien "Cho xac
# nhan" tren trang Canh bao. Bat tham so qua delegate, khong cham DB.
$script:batNguoi = $null; $script:batKhoa = $null
$bat = [Func[string, string, int]] { param($k, $n) $script:batKhoa = $k; $script:batNguoi = $n; return 1 }
$that = New-Object TotalParking.Services.Plc.CanhBaoD3Store($bat)
$that.Dong('PLC_D3:7:102')
$ok = ($script:batKhoa -eq 'PLC_D3:7:102') -and ($script:batNguoi -eq [TotalParking.Services.CanhBaoRepository]::NguoiXacNhanTuDong) -and ($script:batNguoi -eq 'System')
Ket-Qua 'dong_kem_xac_nhan' $ok ("khoa={0} nguoi_xac_nhan={1}" -f $script:batKhoa, $script:batNguoi)

# ---------------------------------------------------------------- live
# Doc D3 that 3 lan cach ~1s, giu cac khoi doc DU 3 lan va ra CUNG mot gia tri.
# Khoi co ma trong bang -> phai co dung khoa PLC_D3:<khoi>:<ma> dang mo; khoi
# con lai -> khong co khoa nao. So lech thi doc lai mot luot de loai truong hop
# D3 vua doi giua luc probe doc va luc may chu kip on dinh.
function Chuoi-Ket-Noi {
    $web = 'C:\Users\Admin\Documents\Web\totalParking\Web.config'
    [xml]$x = Get-Content -LiteralPath $web -Raw -Encoding UTF8
    $cs = $x.configuration.connectionStrings.add | Where-Object { $_.name -eq 'TotalParkingDb' }
    return $cs.connectionString
}

# mysql.exe chu khong MySqlConnector: ngoai IIS, DLL do khong nap duoc vi thieu
# binding redirect cua Web.config ("type initializer ... ConnectionPool").
# Mat khau di qua bien moi truong MYSQL_PWD, khong nam tren dong lenh.
function Sql-Danh-Sach([string]$sql) {
    $mysql = 'C:\Program Files\MySQL\MySQL Server 8.4\bin\mysql.exe'
    $tham = @{}
    foreach ($phan in (Chuoi-Ket-Noi).Split(';')) {
        $kv = $phan.Split('=', 2)
        if ($kv.Count -eq 2) { $tham[$kv[0].Trim().ToLowerInvariant()] = $kv[1].Trim() }
    }
    $hostName = @($tham['server'], $tham['host'], $tham['data source']) | Where-Object { $_ } | Select-Object -First 1
    $user     = @($tham['user id'], $tham['uid'], $tham['user'], $tham['username']) | Where-Object { $_ } | Select-Object -First 1
    $pwd      = @($tham['password'], $tham['pwd']) | Where-Object { $_ } | Select-Object -First 1
    $db       = @($tham['database']) | Where-Object { $_ } | Select-Object -First 1
    $env:MYSQL_PWD = $pwd
    try {
        $out = & $mysql -h $hostName -u $user -N -B --default-character-set=utf8mb4 $db -e $sql
        if ($LASTEXITCODE -ne 0) { throw "mysql.exe thoat $LASTEXITCODE" }
    } finally { Remove-Item Env:\MYSQL_PWD -ErrorAction SilentlyContinue }
    $ra = @()
    foreach ($dong in @($out)) { if ($dong -ne $null -and $dong -ne '') { $ra += ,@($dong.Split("`t")) } }
    return ,$ra
}

function Doc-D3-Mot-Luot($plcs) {
    $DM = [TotalParking.Services.Plc.PlcMemoryArea]::DM
    $kq = @{}
    foreach ($p in $plcs) {
        $c = New-Object TotalParking.Services.Plc.OmronFinsUdpClient
        try {
            [void]$c.ConnectAsync($p[1], 9600, 1500).GetAwaiter().GetResult()
            $kq[[int]$p[0]] = [int]$c.ReadWordsAsync($DM, 3, 1, 1500).GetAwaiter().GetResult()[0]
        } catch { } finally { $c.Dispose() }
    }
    return $kq
}

function Lech($plcs, $bang) {
    $l1 = Doc-D3-Mot-Luot $plcs; $l2 = Doc-D3-Mot-Luot $plcs; $l3 = Doc-D3-Mot-Luot $plcs
    Start-Sleep -Seconds 4   # may chu chong rung 3 nhip ~3s
    $mo = Sql-Danh-Sach "SELECT khoa_chong_trung FROM canh_bao WHERE het_luc IS NULL AND khoa_chong_trung LIKE 'PLC_D3:%'"
    $khoaMo = @{}; foreach ($h in $mo) { $khoaMo[[string]$h[0]] = $true }
    $onDinh = 0; $coMa = 0; $sai = @()
    foreach ($k in $l1.Keys) {
        if (-not ($l2.ContainsKey($k) -and $l3.ContainsKey($k))) { continue }
        $v = $l1[$k]; if ($l2[$k] -ne $v -or $l3[$k] -ne $v) { continue }
        $onDinh++
        $can = if ($bang.ContainsKey($v)) { "PLC_D3:${k}:$v" } else { $null }
        if ($can) { $coMa++ }
        $coKhoa = @($khoaMo.Keys | Where-Object { $_ -like "PLC_D3:${k}:*" })
        $dung = if ($can) { ($coKhoa.Count -eq 1 -and $coKhoa[0] -eq $can) } else { $coKhoa.Count -eq 0 }
        if (-not $dung) { $sai += ("khoi {0}: D3={1} mo=[{2}]" -f $k, $v, ($coKhoa -join ',')) }
    }
    return @{ OnDinh = $onDinh; CoMa = $coMa; Sai = $sai; TongMo = $khoaMo.Count }
}

try {
    $plcs = Sql-Danh-Sach "SELECT b.block_no, d.ip_address FROM plc_device d JOIN block b USING(block_id) WHERE d.is_active = 1 AND b.is_active = 1 ORDER BY b.block_no"
    $lan1 = Lech $plcs $bang
    $sai = $lan1.Sai
    if ($sai.Count -gt 0) { $sai = (Lech $plcs $bang).Sai }
    $dongKhongXacNhan = Sql-Danh-Sach "SELECT COUNT(*) FROM canh_bao WHERE ma_loi LIKE 'PLC-D3-%' AND het_luc IS NOT NULL AND (xac_nhan_luc IS NULL OR xac_nhan_boi IS NULL)"
    $dongTong = Sql-Danh-Sach "SELECT COUNT(*) FROM canh_bao WHERE ma_loi LIKE 'PLC-D3-%' AND het_luc IS NOT NULL"
    $dkx = [int]$dongKhongXacNhan[0][0]
    $chiTiet = ("plc={0} on_dinh={1} co_ma_trong_bang={2} khoa_dang_mo={3} lech={4} da_dong={5} da_dong_chua_xac_nhan={6}{7}" -f `
        $plcs.Count, $lan1.OnDinh, $lan1.CoMa, $lan1.TongMo, $sai.Count, $dongTong[0][0], $dkx, $(if ($sai) { ' | ' + (($sai | Select-Object -First 10) -join '; ') } else { '' }))
    if ($lan1.CoMa -eq 0) {
        Write-Output ("UNVERIFIED live: khong quan sat duoc ma nao trong bang. {0}" -f $chiTiet)
        $script:failed++
    } else {
        Ket-Qua 'live' ($sai.Count -eq 0 -and $dkx -eq 0) $chiTiet
    }
} catch {
    Ket-Qua 'live' $false ("loi: " + $_.Exception.Message)
}

Write-Output ''
if ($script:failed -eq 0 -and $script:passed -eq $SoCa) { Write-Output ("TONG KET: {0}/{1} PASS" -f $script:passed, $SoCa); exit 0 }
Write-Output ("TONG KET: {0} PASS, {1} FAIL / {2} ca" -f $script:passed, $script:failed, $SoCa); exit 1
