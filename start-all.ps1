<#
.SYNOPSIS
  Mock OBİS, backend API ve frontend'i tek komutla başlatır (Windows PowerShell 5.1+ / PowerShell 7).

.DESCRIPTION
  1. Gereksinimleri (dotnet, node, npm) kontrol eder.
  2. Frontend bağımlılıklarını (ilk çalıştırmada) kurar, backend'i derler.
  3. Üç süreci arka planda başlatır; günlükler .run/ klasörüne yazılır.
  4. Hazır olana kadar bekler ve adresleri/demo hesapları yazdırır.

  Durdurmak için: .\stop-all.ps1

.EXAMPLE
  .\start-all.ps1
#>
[CmdletBinding()]
param(
    [int]$ObisPort = 5280,
    [int]$ApiPort = 5154,
    [int]$WebPort = 5173,
    [int]$TimeoutSeconds = 90
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$run = Join-Path $root '.run'
$pidFile = Join-Path $run 'pids.json'
New-Item -ItemType Directory -Force -Path $run | Out-Null

function Test-Port([int]$Port) {
    try {
        $c = New-Object System.Net.Sockets.TcpClient
        $iar = $c.BeginConnect('127.0.0.1', $Port, $null, $null)
        $ok = $iar.AsyncWaitHandle.WaitOne(300) -and $c.Connected
        $c.Close()
        return $ok
    } catch { return $false }
}

function Wait-Url([string]$Name, [string]$Url, [int]$Timeout) {
    $deadline = (Get-Date).AddSeconds($Timeout)
    while ((Get-Date) -lt $deadline) {
        try {
            # 401/404 de "ayakta" demektir; yalnızca bağlantı hatası bekletir.
            Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop | Out-Null
            return
        } catch {
            if ($_.Exception.Response) { return }
        }
        Start-Sleep -Milliseconds 500
    }
    throw "$Name $Timeout sn içinde hazır olmadı. Günlüğe bakın: $run"
}

# --- 1) gereksinimler
foreach ($cmd in 'dotnet', 'node', 'npm') {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
        throw "'$cmd' bulunamadı. .NET 8+ SDK ve Node.js 20+ kurulu olmalı (bkz. README)."
    }
}

# --- 2) zaten çalışıyor mu?
if (Test-Path $pidFile) {
    $old = Get-Content $pidFile -Raw | ConvertFrom-Json
    $alive = @($old.PSObject.Properties.Value | Where-Object { Get-Process -Id $_ -ErrorAction SilentlyContinue })
    if ($alive.Count -gt 0) {
        throw "Uygulama zaten çalışıyor görünüyor. Önce .\stop-all.ps1 çalıştırın."
    }
}
foreach ($p in @($ObisPort, $ApiPort, $WebPort)) {
    if (Test-Port $p) { throw "Port $p dolu. Başka bir süreç kullanıyor olabilir (ya da önceki çalıştırma kapatılmamış: .\stop-all.ps1)." }
}

# --- 3) hazırlık
if (-not (Test-Path (Join-Path $root 'frontend/node_modules'))) {
    Write-Host '> Frontend bağımlılıkları kuruluyor (ilk çalıştırma)...' -ForegroundColor Cyan
    Push-Location (Join-Path $root 'frontend'); npm install --no-audit --no-fund; Pop-Location
}
Write-Host '> Backend derleniyor...' -ForegroundColor Cyan
dotnet build (Join-Path $root 'backend/OgrenciDanismanOtomasyonu.slnx') -nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Backend derlenemedi.' }

# --- 4) süreçleri başlat
function Start-Bg([string]$Name, [string]$File, [string[]]$ArgList, [string]$WorkDir) {
    $out = Join-Path $run "$Name.log"
    $err = Join-Path $run "$Name.err.log"
    Start-Process -FilePath $File -ArgumentList $ArgList -WorkingDirectory $WorkDir `
        -RedirectStandardOutput $out -RedirectStandardError $err -WindowStyle Hidden -PassThru
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:VITE_API_TARGET = "http://localhost:$ApiPort"

$env:ASPNETCORE_URLS = "http://localhost:$ObisPort"
$obis = Start-Bg 'obis' 'dotnet' @('run', '--no-build', '--no-launch-profile') (Join-Path $root 'backend/src/Obis.MockApi')

$env:ASPNETCORE_URLS = "http://localhost:$ApiPort"
$env:Obis__BaseUrl = "http://localhost:$ObisPort"
$api = Start-Bg 'api' 'dotnet' @('run', '--no-build', '--no-launch-profile') (Join-Path $root 'backend/src/Advisor.Api')

$web = Start-Bg 'web' 'cmd.exe' @('/c', "npm run dev -- --port $WebPort --strictPort") (Join-Path $root 'frontend')

@{ obis = $obis.Id; api = $api.Id; web = $web.Id } | ConvertTo-Json | Set-Content $pidFile

# --- 5) hazır olana kadar bekle
Write-Host '> Servislerin hazır olması bekleniyor...' -ForegroundColor Cyan
Wait-Url 'Mock OBİS' "http://localhost:$ObisPort/api/advisors" $TimeoutSeconds
Wait-Url 'Backend API' "http://localhost:$ApiPort/api/auth/me" $TimeoutSeconds
Wait-Url 'Frontend' "http://localhost:$WebPort/" $TimeoutSeconds

Write-Host ''
Write-Host 'Hazır.' -ForegroundColor Green
Write-Host "  Uygulama   : http://localhost:$WebPort"
Write-Host "  Swagger    : http://localhost:$ApiPort/swagger"
Write-Host "  Mock OBİS  : http://localhost:$ObisPort/swagger"
Write-Host '  Demo hesap : admin / danisman1 / ogrenci2  (şifre hepsi: Demo123!)'
Write-Host "  Günlükler  : $run"
Write-Host '  Durdurmak  : .\stop-all.ps1'
