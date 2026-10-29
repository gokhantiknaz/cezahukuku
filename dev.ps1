# Geliştirme ortamını başlatır: önce API (ayrı pencerede), API hazır olunca web.
# Kullanım: .\dev.ps1   (ya da dev.cmd'ye çift tıklayın)
# Web bu pencerede çalışır, Ctrl+C ile durur; API kendi penceresinde kalır, o pencereyi kapatınca durur.

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$apiUrl = "http://localhost:5080"
$webPort = 3000

function Test-Port([int]$port) {
    [bool](Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)
}

function Test-Api {
    try { (Invoke-WebRequest "$apiUrl/api/health" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200 } catch { $false }
}

if (Test-Api) {
    Write-Host "API zaten çalışıyor ($apiUrl)." -ForegroundColor Green
} elseif (Test-Port 5080) {
    Write-Host "5080 portu kullanımda ama API yanıt vermiyor; o süreci kapatıp yeniden deneyin." -ForegroundColor Red
    exit 1
} else {
    Write-Host "API başlatılıyor (ayrı pencerede)..." -ForegroundColor Cyan
    Start-Process powershell -WorkingDirectory $root -ArgumentList @(
        "-NoExit", "-Command",
        "`$Host.UI.RawUI.WindowTitle = 'Ceza Hukukçusu API'; dotnet run --project backend/src/CezaHukuku.Api")

    # İlk derleme bir dakikayı bulabilir.
    $deadline = (Get-Date).AddMinutes(3)
    while (-not (Test-Api)) {
        if ((Get-Date) -gt $deadline) {
            Write-Host "`nAPI 3 dakikada hazır olmadı; API penceresindeki hatalara bakın." -ForegroundColor Red
            exit 1
        }
        Write-Host "." -NoNewline
        Start-Sleep -Seconds 2
    }
    Write-Host "`nAPI hazır ($apiUrl, API dokümanı: $apiUrl/scalar)." -ForegroundColor Green
}

if (Test-Port $webPort) {
    Write-Host "Web zaten çalışıyor: http://localhost:$webPort" -ForegroundColor Green
    exit 0
}

Write-Host "Web başlatılıyor: http://localhost:$webPort (durdurmak için Ctrl+C)" -ForegroundColor Cyan
Set-Location (Join-Path $root "web")
npm run dev
