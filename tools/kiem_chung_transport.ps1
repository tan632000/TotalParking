# Kiem chung chon transport TCP/UDP theo block (specs/fins-udp/task-02, task-03).
#
#   -UdpBlocks  gia tri ky vong cua plc:udpBlocks trong Web.config BAN DEPLOY.
#               Bo tham so = ky vong moi block dung TCP.
#               (PowerShell 5.1 bo doi so rong khi goi qua -File, nen dung truyen
#               -UdpBlocks "" — cu bo han tham so.)
#
# Chay tu thu muc goc repo, sau khi build va deploy:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_transport.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_transport.ps1 -UdpBlocks "1,2"
#
# Thoat 0 chi khi moi kiem tra PASS.

param([string]$UdpBlocks = "")

$ErrorActionPreference = 'Stop'
$script:failed = 0
$script:passed = 0

function Ket-Qua([string]$ten, [bool]$ok, [string]$chiTiet) {
    if ($ok) { Write-Output ("PASS  {0,-28} {1}" -f $ten, $chiTiet); $script:passed++ }
    else     { Write-Output ("FAIL  {0,-28} {1}" -f $ten, $chiTiet); $script:failed++ }
}

$dll = Join-Path $PSScriptRoot '..\TotalParking\bin\TotalParking.dll'
[void][Reflection.Assembly]::LoadFrom((Resolve-Path $dll))
$Conn = [TotalParking.Services.Plc.PlcConnection]

# KHONG khai bao [string]$raw: PowerShell se ep $null thanh "" va ca <null> chi
# con la ca "" lap lai. [NullString]::Value moi truyen null that xuong .NET.
function Tach($raw) {
    $all = $false
    $doiSo = if ($raw -eq $null) { [NullString]::Value } else { [string]$raw }
    $set = $Conn::ParseUdpBlocks($doiSo, [ref]$all)
    return @{ Set = @($set | Sort-Object); All = $all }
}

# ---------------------------------------------------------------- parse (offline)
# Moi ca: gia tri cau hinh -> tap ky vong + all ky vong. Ca rac phai bi bo qua
# chu khong nem loi: ham nay chay trong constructor cua ca 112 block.
$caParse = @(
    @{ Raw = $null;      Set = @();     All = $false },
    @{ Raw = '';         Set = @();     All = $false },
    @{ Raw = '1,2';      Set = @(1, 2); All = $false },
    @{ Raw = ' 1 , 2 ';  Set = @(1, 2); All = $false },
    @{ Raw = '1;2';      Set = @();     All = $false },
    @{ Raw = 'b1,3';     Set = @(3);    All = $false },
    @{ Raw = '*';        Set = @();     All = $true  }
)
foreach ($ca in $caParse) {
    $ten = 'parse ' + $(if ($ca.Raw -eq $null) { '<null>' } else { '"' + $ca.Raw + '"' })
    try {
        $kq = Tach $ca.Raw
        $ok = (($kq.Set -join ',') -eq ($ca.Set -join ',')) -and ($kq.All -eq $ca.All)
        Ket-Qua $ten $ok ("tap={{{0}}} all={1}" -f ($kq.Set -join ','), $kq.All)
    } catch {
        Ket-Qua $ten $false ("nem loi: " + $_.Exception.InnerException.Message)
    }
}

# ---------------------------------------------------------------- transport (live)
$ky = Tach $UdpBlocks
try {
    $j = Invoke-RestMethod http://localhost:8080/PlcStatus -TimeoutSec 30
} catch {
    Ket-Qua 'transport' $false ("khong doc duoc /PlcStatus: " + $_.Exception.Message)
    $j = $null
}

if ($j -ne $null) {
    $blocks = @($j.blocks)
    $thieu  = @($blocks | ? { -not ($_.PSObject.Properties.Name -contains 'transport') })
    $sai    = @()
    $soUdp  = 0
    foreach ($b in $blocks) {
        $mong = if ($ky.All -or ($ky.Set -contains [int]$b.block_no)) { 'udp' } else { 'tcp' }
        if ($b.transport -eq 'udp') { $soUdp++ }
        if ($b.transport -ne $mong) { $sai += ("{0}:{1}(mong {2})" -f $b.block_no, $b.transport, $mong) }
    }
    # 112 block la so PLC dang van hanh; nap thieu block cung phai FAIL.
    Ket-Qua 'transport co trong JSON' ($blocks.Count -eq 112 -and $thieu.Count -eq 0) ("{0}/112 block, thieu truong transport: {1}" -f $blocks.Count, $thieu.Count)
    Ket-Qua 'transport dung cau hinh' ($blocks.Count -gt 0 -and $sai.Count -eq 0) ("ky_vong udpBlocks='{0}' -> udp={1} tcp={2}; sai: {3}" -f $UdpBlocks, $soUdp, ($blocks.Count - $soUdp), $(if ($sai.Count) { "$($sai.Count) block: " + (($sai | Select-Object -First 10) -join ' ') } else { 'khong' }))
}

# ---------------------------------------------------------------- thi diem (task 03)
# Chi chay khi co block ky vong dung UDP. Doc tu ban deploy, khong ghi gi.
$deploy = 'C:\Users\Admin\Documents\Web\totalParking'
# Lay theo block KY VONG chay UDP, khong theo block DANG bao "udp": neu cau hinh
# khong duoc ap dung thi danh sach sau se rong va moi kiem tra ben duoi PASS rong.
$blockThiDiem = @()
if ($j -ne $null) { $blockThiDiem = @($j.blocks | ? { $ky.All -or ($ky.Set -contains [int]$_.block_no) }) }

if (($ky.All -or $ky.Set.Count -gt 0) -and $j -ne $null) {
    $ips = @($blockThiDiem | % { ($_.endpoint -split ':')[0] })
    # Block ky vong ma vang mat trong JSON thi moi vong kiem duoi day chay 0 lan
    # va PASS rong — phai FAIL o day.
    Ket-Qua 'block thi diem co trong JSON' ($blockThiDiem.Count -gt 0 -and ($ky.All -or $blockThiDiem.Count -eq $ky.Set.Count)) ("ky_vong={0} thay={1}" -f $(if ($ky.All) { '*' } else { $ky.Set.Count }), $blockThiDiem.Count)

    # MOC = lan cuoi Web.config hoac DLL ban deploy doi = luc AppDomain hien tai
    # khoi dong (w3wp KHONG khoi dong lai khi deploy). Khong cong bien: AppDomain
    # moi ghi D1004 o nhip poll dau, ~5 s sau moc.
    $moc = @((Get-Item "$deploy\Web.config").LastWriteTime, (Get-Item "$deploy\bin\TotalParking.dll").LastWriteTime) | Sort-Object | Select-Object -Last 1
    $tuoi = ((Get-Date) - $moc).TotalSeconds
    Ket-Qua 'chay sau moc >= 30s' ($tuoi -ge 30) ("moc={0:yyyy-MM-dd HH:mm:ss} da qua {1:N0}s" -f $moc, $tuoi)

    # online + last_ok moi (so voi gio cua chinh may chu trong /PlcStatus)
    $now = [datetime]::ParseExact($j.now, 'yyyy-MM-dd HH:mm:ss', $null)
    foreach ($b in $blockThiDiem) {
        $moi = $false
        if ($b.last_ok) { $moi = ($now - [datetime]::ParseExact($b.last_ok, 'yyyy-MM-dd HH:mm:ss', $null)).TotalSeconds -le 10 }
        Ket-Qua ("online block {0}" -f $b.block_no) ($b.online -and $moi) ("online={0} last_ok={1} loi={2}" -f $b.online, $b.last_ok, $b.error)
    }

    # Socket: tien trinh w3wp KHONG con giu TCP ESTABLISHED toi PLC dang chay UDP.
    # PlcReachabilityScanner mo-roi-dong TCP moi 5 phut, nen chi FAIL khi CA 3
    # mau (cach 5 s) deu thay.
    $w3 = @(Get-CimInstance Win32_Process -Filter "Name='w3wp.exe'" | % { [int]$_.ProcessId })
    $thay = 0
    for ($i = 0; $i -lt 3; $i++) {
        if ($i -gt 0) { Start-Sleep -Seconds 5 }
        $c = @(Get-NetTCPConnection -RemotePort 9600 -State Established -ErrorAction SilentlyContinue |
               ? { ($w3 -contains [int]$_.OwningProcess) -and ($ips -contains $_.RemoteAddress) })
        if ($c.Count -gt 0) { $thay++ }
    }
    Ket-Qua 'khong con TCP toi block UDP' ($w3.Count -gt 0 -and $thay -lt 3) ("w3wp={0} mau_thay_TCP={1}/3 ip={2}" -f ($w3 -join ','), $thay, ($ips -join ','))

    # Ghi qua UDP: plc_audit.log co WRITE ... OK cho tung IP sau MOC, va khong
    # co ERROR nao cho IP do sau MOC. Khong co dong WRITE = CHUA CHUNG MINH.
    # Log xoay sang .1 khi qua 8 MB (PlcAuditLog): neu .1 doi sau MOC thi phan
    # dau cua cua so nam trong do, doc ca hai.
    $logs = @("$deploy\App_Data\plc_audit.log.1", "$deploy\App_Data\plc_audit.log") |
            ? { (Test-Path $_) -and (Get-Item $_).LastWriteTime -ge $moc }
    # So toi mili-giay: log ghi "yyyy-MM-dd HH:mm:ss.fff".
    $sauMoc = @($logs | % { Get-Content $_ -Encoding UTF8 } | ? {
        $_ -match '^(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d\.\d{3})' -and
        [datetime]::ParseExact($matches[1], 'yyyy-MM-dd HH:mm:ss.fff', $null) -ge $moc
    })
    foreach ($ip in $ips) {
        $re  = [regex]::Escape($ip) + '\s'
        $ok  = @($sauMoc | ? { $_ -match "^\S+ \S+\s+WRITE\s+$re" -and $_ -cmatch '\sOK(\s\s\||\s*$)' })
        # Lenh ghi hong ghi "WRITE ... LOI | <loi>" chu khong phai ERROR
        # (PlcConnection: duong ghi D1000).
        $loi = @($sauMoc | ? { $_ -match "^\S+ \S+\s+WRITE\s+$re" -and $_ -cmatch '\sLOI(\s\s\||\s*$)' })
        $errAll = @($sauMoc | ? { $_ -match "^\S+ \S+\s+ERROR\s+$re" })
        # AppDomain CU dang tat van con nhip poll do dang -> ERROR "The semaphore
        # has been disposed." ngay sau MOC (do 05/10: 104 dong luc 23:29:42.7, moc
        # 23:29:42) — _gate cua PlcConnection trong domain TCP cu, khong phai duong
        # UDP. Chi loai DUNG chu ky nay, trong 10 s dau, va in so luong; moi loi
        # khac (ke ca ObjectDisposedException cua socket UDP) van FAIL.
        $tat = @($errAll | ? {
            $_ -match '\|\s*The semaphore has been disposed\.' -and
            [datetime]::ParseExact($_.Substring(0, 23), 'yyyy-MM-dd HH:mm:ss.fff', $null) -le $moc.AddSeconds(10)
        })
        $err = @($errAll | ? { $tat -notcontains $_ })
        $trangThai = if ($ok.Count -eq 0) { 'CHUA CHUNG MINH' } else { "{0} dong WRITE OK" -f $ok.Count }
        $mau = @($err + $loi) | Select-Object -First 1
        Ket-Qua ("ghi qua UDP {0}" -f $ip) ($ok.Count -gt 0 -and $err.Count -eq 0 -and $loi.Count -eq 0) ("{0}; WRITE LOI: {1}; ERROR sau moc: {2} (loai {3} dong domain cu dang tat){4}" -f $trangThai, $loi.Count, $err.Count, $tat.Count, $(if ($mau) { ' | ' + $mau } else { '' }))
    }
}

Write-Output ''
if ($script:failed -eq 0 -and $script:passed -gt 0) {
    Write-Output ("TONG KET: {0}/{0} PASS" -f $script:passed); exit 0
}
Write-Output ("TONG KET: {0} PASS, {1} FAIL" -f $script:passed, $script:failed); exit 1
