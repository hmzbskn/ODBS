<#
.SYNOPSIS
  start-all.ps1 ile başlatılan Mock OBİS, API ve frontend süreçlerini (alt süreçleriyle) durdurur.
#>
$ErrorActionPreference = 'Continue'
$pidFile = Join-Path $PSScriptRoot '.run/pids.json'

if (-not (Test-Path $pidFile)) {
    Write-Host 'Çalışan kayıtlı süreç yok.'
    return
}

$pids = Get-Content $pidFile -Raw | ConvertFrom-Json
foreach ($name in 'web', 'api', 'obis') {
    $id = $pids.$name
    if ($id -and (Get-Process -Id $id -ErrorAction SilentlyContinue)) {
        # /T: alt süreçler de (npm -> node, dotnet -> uygulama) kapanır.
        & taskkill.exe /PID $id /T /F | Out-Null
        Write-Host "Durduruldu: $name (PID $id)"
    }
}

Remove-Item $pidFile -Force
Write-Host 'Tamam.'
