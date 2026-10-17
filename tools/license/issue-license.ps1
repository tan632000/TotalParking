<#
    issue-license.ps1 - cong cu OFFLINE phat hanh va kiem tra license cho SCADA.

    Hop dong ky (phai khop C# verifier trong TotalParking/Services/License):
      - RSA-2048, chu ky RSA.VerifyData(data, SHA256, Pkcs1).
      - Payload canonical (CHINH XAC cac byte duoc ky) la chuoi JSON UTF-8:
          {"customerId":"<id>","issuedUtc":"<iso>","expiryUtc":"<iso>"}
        khoa dung thu tu tren, ngay dang ISO-8601 'yyyy-MM-ddTHH:mm:ssZ'
        (InvariantCulture, UTC), KHONG khoang trang, KHONG xuong dong.
      - File .lic gom 2 dong: dong 1 = payload canonical, dong 2 = Base64 chu ky.

    Luu key: ToXmlString/FromXmlString chi co o RSACryptoServiceProvider, con
    SHA-256 lai an toan o RSACng tren .NET Framework 4.7.2. Nen ta luu/doc key
    bang RSACryptoServiceProvider (XML) roi IMPORT sang RSACng de ky/verify.

    Che do:
      -GenerateKeys    : sinh keypair, ghi private (gitignore) + nhet public key
                         (Base64) vao license:publicKey trong Web.config.
      -Issue           : ky mot license voi -Expiry (yyyy-MM-dd) ra -Out.
      -VerifyCommitted : verify mot -License voi public key doc tu Web.config.

    LUU Y: file nay thuan ASCII, tranh ky tu Unicode vi PowerShell 5.1 doc .ps1
    khong BOM theo ANSI.
#>
#requires -Version 5.1
[CmdletBinding(DefaultParameterSetName='Issue')]
param(
    [Parameter(ParameterSetName='GenerateKeys', Mandatory=$true)][switch]$GenerateKeys,
    [Parameter(ParameterSetName='Issue', Mandatory=$true)][switch]$Issue,
    [Parameter(ParameterSetName='VerifyCommitted', Mandatory=$true)][switch]$VerifyCommitted,

    [string]$CustomerId = 'LUMI-TOTALPARKING',
    [string]$Expiry,
    [string]$Out,
    [string]$License,
    [string]$PublicKeyFrom,
    [string]$KeyDir,
    [string]$ConfigPath,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# $PSScriptRoot khong on dinh trong khoi param, nen resolve o day.
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
if (-not $KeyDir)     { $KeyDir     = Join-Path $scriptDir 'keys' }
if (-not $ConfigPath) { $ConfigPath = Join-Path $scriptDir '..\..\TotalParking\Web.config' }

$PrivPath = Join-Path $KeyDir 'private-key.xml'
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Get-CanonicalJson($customerId, $issuedUtc, $expiryUtc) {
    # Noi chuoi thu cong, KHONG dung ConvertTo-Json, de kiem soat tung byte.
    '{"customerId":"' + $customerId + '","issuedUtc":"' + $issuedUtc + '","expiryUtc":"' + $expiryUtc + '"}'
}

function Import-RsaCng([string]$xml, [bool]$includePrivate) {
    # XML -> RSAParameters (qua RSACryptoServiceProvider) -> RSACng (de SHA-256).
    $csp = New-Object System.Security.Cryptography.RSACryptoServiceProvider
    try {
        $csp.FromXmlString($xml)
        $p = $csp.ExportParameters($includePrivate)
    } finally { $csp.Dispose() }
    $cng = New-Object System.Security.Cryptography.RSACng
    $cng.ImportParameters($p)
    return $cng
}

function Get-PublicKeyB64FromConfig([string]$configPath) {
    $txt = [System.IO.File]::ReadAllText($configPath)
    $m = [regex]::Match($txt, '<add key="license:publicKey"\s+value="([^"]*)"')
    if (-not $m.Success) { throw "Khong tim thay license:publicKey trong $configPath" }
    return $m.Groups[1].Value
}

function Read-LicenseFile([string]$path) {
    $lines = [System.IO.File]::ReadAllLines($path, $Utf8NoBom)
    if ($lines.Count -lt 2) { throw ("File license hong: can 2 dong, co " + $lines.Count) }
    [pscustomobject]@{ Payload = $lines[0]; SignatureB64 = $lines[1] }
}

function Test-Signature([string]$pubXml, [string]$payload, [string]$sigB64) {
    $rsa = Import-RsaCng $pubXml $false
    try {
        $data = [System.Text.Encoding]::UTF8.GetBytes($payload)
        $sig  = [Convert]::FromBase64String($sigB64)
        $rsa.VerifyData($data, $sig,
            [System.Security.Cryptography.HashAlgorithmName]::SHA256,
            [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    } finally { $rsa.Dispose() }
}

# ----------------------------------------------------------------------------
if ($GenerateKeys) {
    if ((Test-Path $PrivPath) -and -not $Force) {
        throw ("Da co private key tai " + $PrivPath + ". Dung -Force de ghi de (SE phai phat hanh lai moi license cu).")
    }
    New-Item -ItemType Directory -Force -Path $KeyDir | Out-Null
    $gen = New-Object System.Security.Cryptography.RSACryptoServiceProvider(2048)
    try {
        $privXml = $gen.ToXmlString($true)
        $pubXml  = $gen.ToXmlString($false)
    } finally { $gen.Dispose() }

    [System.IO.File]::WriteAllText($PrivPath, $privXml, $Utf8NoBom)
    $pubB64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($pubXml))

    # Nhet public key vao Web.config bang thay the co huong (giu nguyen encoding/BOM).
    $bytes = [System.IO.File]::ReadAllBytes($ConfigPath)
    $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    $txt = [System.IO.File]::ReadAllText($ConfigPath)
    $new = [regex]::Replace($txt, '(<add key="license:publicKey"\s+value=")[^"]*(")', ('${1}' + $pubB64 + '${2}'))
    if ($new -eq $txt) { throw ("Khong thay license:publicKey de ghi trong " + $ConfigPath) }
    [System.IO.File]::WriteAllText($ConfigPath, $new, (New-Object System.Text.UTF8Encoding($hasBom)))

    Write-Host "GENERATED KEYS"
    Write-Host ("  private: " + $PrivPath + "  (gitignore - SAO LUU va GIU OFFLINE)")
    Write-Host ("  public : da ghi vao license:publicKey trong " + $ConfigPath)
    exit 0
}

if ($Issue) {
    if (-not $Expiry) { throw "-Issue can -Expiry yyyy-MM-dd" }
    if (-not $Out)    { throw "-Issue can -Out duong-dan-.lic" }
    if (-not (Test-Path $PrivPath)) { throw "Chua co private key. Chay -GenerateKeys truoc." }

    $exp = [datetime]::ParseExact($Expiry, 'yyyy-MM-dd', [System.Globalization.CultureInfo]::InvariantCulture)
    $expUtc = [datetime]::SpecifyKind($exp, [System.DateTimeKind]::Utc)
    $issuedUtc = [datetime]::UtcNow
    $fmt = 'yyyy-MM-ddTHH:mm:ssZ'
    $inv = [System.Globalization.CultureInfo]::InvariantCulture
    $payload = Get-CanonicalJson $CustomerId $issuedUtc.ToString($fmt, $inv) $expUtc.ToString($fmt, $inv)

    $rsa = Import-RsaCng ([System.IO.File]::ReadAllText($PrivPath)) $true
    try {
        $sig = $rsa.SignData([System.Text.Encoding]::UTF8.GetBytes($payload),
            [System.Security.Cryptography.HashAlgorithmName]::SHA256,
            [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    } finally { $rsa.Dispose() }
    $sigB64 = [Convert]::ToBase64String($sig)

    $dir = Split-Path -Parent $Out
    if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    [System.IO.File]::WriteAllLines($Out, @($payload, $sigB64), $Utf8NoBom)

    Write-Host "ISSUED LICENSE"
    Write-Host ("  customer: " + $CustomerId)
    Write-Host ("  expiry  : " + $expUtc.ToString($fmt))
    Write-Host ("  out     : " + $Out)
    exit 0
}

if ($VerifyCommitted) {
    if (-not $License)       { throw "-VerifyCommitted can -License .lic" }
    if (-not $PublicKeyFrom) { $PublicKeyFrom = $ConfigPath }

    $pubB64 = Get-PublicKeyB64FromConfig $PublicKeyFrom
    if ($pubB64 -eq 'PASTE_BASE64_PUBLIC_KEY' -or [string]::IsNullOrWhiteSpace($pubB64)) {
        throw "license:publicKey chua duoc dien. Chay -GenerateKeys truoc."
    }
    $pubXml = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($pubB64))
    $lic = Read-LicenseFile $License

    $ok = Test-Signature $pubXml $lic.Payload $lic.SignatureB64
    $expiry = ([regex]::Match($lic.Payload, '"expiryUtc":"([^"]*)"')).Groups[1].Value
    if ($ok) {
        Write-Host "COMMITTED LICENSE: VALID"
        Write-Host ("  expiry: " + $expiry)
        exit 0
    } else {
        Write-Host "COMMITTED LICENSE: INVALID (chu ky khong khop public key)"
        exit 1
    }
}
