# Kiem chung OmronFinsUdpClient (specs/fins-udp/task-01-client-udp.md).
#
# 8 ca, moi ca in PASS/FAIL. Thoat 0 chi khi DEM DU 8 PASS va khong ca nao FAIL.
#   live_*      doc PLC THAT (chi DOC, khong ghi gi xuong PLC that)
#   loopback_*  dung mot "PLC gia" tren 127.0.0.1 de tao nhung tinh huong mang
#               that hiem khi tao ra: mat goi, goi SID cu, khong tra loi lenh ghi.
#
# Chay tu thu muc goc repo, SAU khi build:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_fins_udp.ps1

$ErrorActionPreference = 'Stop'

$dll = Join-Path $PSScriptRoot '..\TotalParking\bin\TotalParking.dll'
# LoadFrom chu khong Add-Type -Path: Add-Type duyet moi kieu trong DLL web va co
# the vap phu thuoc MVC/MySqlConnector khong lien quan toi bai kiem.
[void][Reflection.Assembly]::LoadFrom((Resolve-Path $dll))

# ---------------------------------------------------------------- PLC gia
# Viet bang C# 5 (trinh bien dich cua PowerShell 5.1). KHONG tham chieu DLL ung
# dung, de PLC gia khong dung chung ma voi thu dang bi kiem.
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class FakePlc
{
    // Mot datagram da nhan: lenh nao, SID nao, word nao.
    public class Seen { public byte Mrc, Src, Sid; public int Word; }

    private readonly UdpClient _udp;
    private readonly Thread _thread;
    private readonly string _scenario;
    private readonly List<Seen> _seen = new List<Seen>();
    private volatile bool _stop;

    public int Port { get { return ((IPEndPoint)_udp.Client.LocalEndPoint).Port; } }

    public FakePlc(string scenario)
    {
        _scenario = scenario;
        _udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        _thread = new Thread(Loop);
        _thread.IsBackground = true;
        _thread.Start();
    }

    public List<Seen> SeenFor(byte src, int word)
    {
        var r = new List<Seen>();
        lock (_seen) { foreach (var s in _seen) if (s.Src == src && s.Word == word) r.Add(s); }
        return r;
    }

    public void Stop() { _stop = true; _udp.Close(); }

    private void Loop()
    {
        var counts = new Dictionary<string, int>();
        while (!_stop)
        {
            IPEndPoint from = null;
            byte[] d;
            try { d = _udp.Receive(ref from); } catch { return; }
            if (d.Length < 18) continue;

            var s = new Seen { Mrc = d[10], Src = d[11], Sid = d[9], Word = (d[13] << 8) | d[14] };
            lock (_seen) _seen.Add(s);

            string key = s.Src + ":" + s.Word;
            int n; counts.TryGetValue(key, out n); counts[key] = ++n;

            // Goi tham do D0 cua ConnectAsync: luon tra loi.
            if (s.Src == 0x01 && s.Word == 0) { Reply(d, from, d[9], 0); continue; }

            switch (_scenario)
            {
                case "bo_goi_dau":
                    if (s.Src == 0x01 && s.Word == 1000 && n >= 2) Reply(d, from, d[9], 4321);
                    break;
                case "sid_cu":
                    if (s.Src == 0x01 && s.Word == 1000)
                    {
                        Reply(d, from, (byte)(d[9] - 1), 1111);   // goi tre cua lenh truoc
                        Reply(d, from, d[9], 2222);
                    }
                    break;
                case "ghi_khong_tra_loi":
                    break;   // khong tra loi gi ngoai goi tham do
                case "lenh_sau":
                    if (s.Src == 0x01 && s.Word == 1000) Reply(d, from, d[9], 3333);
                    break;   // word 500 (lenh 1): khong bao gio tra loi
            }
        }
    }

    // Phan hoi Memory Area Read 1 word, End Code 0000.
    private void Reply(byte[] req, IPEndPoint to, byte sid, int value)
    {
        var r = new byte[16];
        r[0] = 0xC0; r[1] = 0; r[2] = 0x02;
        r[3] = 0; r[4] = req[7]; r[5] = 0;       // DA = SA cua yeu cau
        r[6] = 0; r[7] = req[4]; r[8] = 0;       // SA = DA cua yeu cau
        r[9] = sid; r[10] = req[10]; r[11] = req[11];
        r[12] = 0; r[13] = 0;
        r[14] = (byte)(value >> 8); r[15] = (byte)(value & 0xFF);
        _udp.Send(r, r.Length, to);
    }
}
'@

# ---------------------------------------------------------------- tien ich
$DM      = [TotalParking.Services.Plc.PlcMemoryArea]::DM
$Framing = [TotalParking.Services.Plc.FinsFramingException]
$EndCode = [TotalParking.Services.Plc.FinsException]
$script:failed = 0
$script:passed = 0
$SoCa = 8

function Ket-Qua([string]$ten, [bool]$ok, [string]$chiTiet) {
    if ($ok) { Write-Output ("PASS  {0,-34} {1}" -f $ten, $chiTiet); $script:passed++ }
    else     { Write-Output ("FAIL  {0,-34} {1}" -f $ten, $chiTiet); $script:failed++ }
}

# Goi mot Task va tra ve ket qua. GetAwaiter().GetResult() de loi goc nam o
# InnerException, khong bi boc trong AggregateException nhu .Result/.Wait().
function Cho($task) { return $task.GetAwaiter().GetResult() }

# Chay mot khoi va tra ve exception GOC (hoac $null neu khong loi).
function Bat-Loi([scriptblock]$khoi) {
    try { & $khoi; return $null }
    catch {
        $e = $_.Exception
        if ($e.InnerException) { return $e.InnerException }
        return $e
    }
}

function Client-Moi { New-Object TotalParking.Services.Plc.OmronFinsUdpClient }

# ---------------------------------------------------------------- live
$plc = Client-Moi
try {
    $null = Cho ($plc.ConnectAsync('192.169.1.101', 9600, 3000))
    $w = Cho ($plc.ReadWordsAsync($DM, 1000, 1, 3000))
    Ket-Qua 'live_doc_D1000_block1' ($w.Length -eq 1 -and $plc.Transport -eq 'udp') ("D1000={0} transport={1}" -f $w[0], $plc.Transport)
} catch { Ket-Qua 'live_doc_D1000_block1' $false $_.Exception.InnerException.Message }

$c = Client-Moi
$sw = [Diagnostics.Stopwatch]::StartNew()
$loi = Bat-Loi { Cho ($c.ConnectAsync('192.169.1.250', 9600, 3000)) }
$sw.Stop(); $s = $sw.Elapsed.TotalSeconds
Ket-Qua 'live_ip_khong_ton_tai' (($loi -is $Framing) -and $s -ge 2.5 -and $s -le 4.5) ("{0:N2}s {1}" -f $s, $(if ($loi) { $loi.GetType().Name } else { 'khong loi' }))

$loi = Bat-Loi { Cho ($plc.ReadWordsAsync($DM, 32767, 1, 3000)) }
$conNoi = $plc.IsConnected
$sauDo = $null
try { $sauDo = Cho ($plc.ReadWordsAsync($DM, 1000, 1, 3000)) } catch { }
Ket-Qua 'live_dia_chi_ngoai_dai' (($loi -is $EndCode) -and $conNoi -and $sauDo -ne $null) ("loi={0} IsConnected={1} doc_tiep={2}" -f $(if ($loi) { $loi.GetType().Name + ' ' + $loi.Message } else { 'khong' }), $conNoi, ($sauDo -ne $null))
$plc.Dispose()

# ---------------------------------------------------------------- loopback
function Voi-Plc-Gia([string]$tenCa, [string]$kichBan, [scriptblock]$khoi) {
    $gia = New-Object FakePlc $kichBan
    $cl  = Client-Moi
    try {
        $null = Cho ($cl.ConnectAsync('127.0.0.1', $gia.Port, 2000))
        & $khoi $gia $cl
    } catch {
        Ket-Qua $tenCa $false ("loi ngoai du kien: " + $_.Exception.InnerException.Message)
    } finally { $cl.Dispose(); $gia.Stop() }
}

Voi-Plc-Gia 'loopback_bo_goi_dau' 'bo_goi_dau' {
    param($gia, $cl)
    $v = Cho ($cl.ReadWordsAsync($DM, 1000, 1, 2000))
    $seen = $gia.SeenFor(1, 1000)
    $sids = @($seen | % { $_.Sid } | Sort-Object -Unique)
    Ket-Qua 'loopback_bo_goi_dau' ($v[0] -eq 4321 -and $seen.Count -eq 2 -and $sids.Count -eq 2) ("gia_tri={0} so_goi={1} sid_khac_nhau={2}" -f $v[0], $seen.Count, $sids.Count)
}

Voi-Plc-Gia 'loopback_sid_cu' 'sid_cu' {
    param($gia, $cl)
    $v = Cho ($cl.ReadWordsAsync($DM, 1000, 1, 2000))
    Ket-Qua 'loopback_sid_cu' ($v[0] -eq 2222) ("gia_tri={0} (2222 = goi dung SID, 1111 = goi cu)" -f $v[0])
}

Voi-Plc-Gia 'loopback_ghi_mot_lan' 'ghi_khong_tra_loi' {
    param($gia, $cl)
    $data = [uint16[]]@(5)
    $loi = Bat-Loi { Cho ($cl.WriteWordsAsync($DM, 1000, $data, 1500)) }
    Start-Sleep -Milliseconds 500   # cho het moi lan gui lai (neu co) toi noi
    $ghi = $gia.SeenFor(2, 1000)
    Ket-Qua 'loopback_ghi_mot_lan' (($loi -is $Framing) -and $ghi.Count -eq 1) ("loi={0} so_goi_ghi={1}" -f $(if ($loi) { $loi.GetType().Name } else { 'khong' }), $ghi.Count)
}

Voi-Plc-Gia 'loopback_lenh_sau_khong_bi_nuot' 'lenh_sau' {
    param($gia, $cl)
    $loi1 = Bat-Loi { Cho ($cl.ReadWordsAsync($DM, 500, 1, 1000)) }
    $loi2 = Bat-Loi { $script:v2 = Cho ($cl.ReadWordsAsync($DM, 1000, 1, 1000)) }
    $lan = $gia.SeenFor(1, 1000).Count
    $ok = ($loi1 -is $Framing) -and ($loi2 -eq $null) -and ($script:v2[0] -eq 3333) -and $lan -eq 1
    Ket-Qua 'loopback_lenh_sau_khong_bi_nuot' $ok ("lenh1={0} lenh2={1} so_lan_gui_lenh2={2}" -f $(if ($loi1) { $loi1.GetType().Name } else { 'khong loi' }), $(if ($loi2) { $loi2.Message } else { $script:v2[0] }), $lan)
}

# PLC tat ngay sau khi ket noi: cong dong, Windows bao ICMP port unreachable
# (10054). Loi nay co the bi nem DONG BO luc tao luot nhan — phai ra
# FinsFramingException chu khong phai SocketException tho.
Voi-Plc-Gia 'loopback_plc_dung' 'dung' {
    param($gia, $cl)
    $gia.Stop()
    Start-Sleep -Milliseconds 200
    $loi = Bat-Loi { Cho ($cl.ReadWordsAsync($DM, 1000, 1, 1000)) }
    Ket-Qua 'loopback_plc_dung' ($loi -is $Framing) ("loi={0}" -f $(if ($loi) { $loi.GetType().Name + ' ' + $loi.Message } else { 'khong loi' }))
}

Write-Output ''
if ($script:failed -eq 0 -and $script:passed -eq $SoCa) { Write-Output ("TONG KET: {0}/{1} PASS" -f $script:passed, $SoCa); exit 0 }
Write-Output ("TONG KET: {0} PASS, {1} FAIL / {2} ca" -f $script:passed, $script:failed, $SoCa); exit 1
