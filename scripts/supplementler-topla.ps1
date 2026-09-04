<#
  Supplementler.com toplayıcısı — geliştirme makinesinde günde bir çalışır.

  NEDEN BU MAKİNEDE: site sunucumuzun bulunduğu datacenter aralığını
  Cloudflare managed challenge ile karşılıyor ("Just a moment..." sayfası),
  ev bağlantısından normal 200 dönüyor. Toplama burada, yutma sunucuda.

  Anahtar DEPODA DEĞİL: C:\Users\<kullanıcı>\.proteinavcisi\ingest.key
  içinde duruyor. Orası bilinçli olarak OneDrive'ın DIŞINDA — sır buluta
  senkronlanmamalı (veritabanı yedeğinde de aynı karar verilmişti).

  Elle çalıştırmak için:
      powershell -ExecutionPolicy Bypass -File scripts\supplementler-topla.ps1
  Yalnızca toplayıp görmek (gönderim yok):
      ... -KuruCalis
#>
[CmdletBinding()]
param(
    [switch]$KuruCalis
)

$ErrorActionPreference = 'Stop'

$depoKok    = Split-Path -Parent $PSScriptRoot
$proje      = Join-Path $depoKok 'backend\src\IndirimTakip.Toplayici\IndirimTakip.Toplayici.csproj'
$anahtarYol = Join-Path $env:USERPROFILE '.proteinavcisi\ingest.key'
$logKlasor  = Join-Path $env:USERPROFILE '.proteinavcisi\log'
$logYol     = Join-Path $logKlasor ('supplementler-{0}.log' -f (Get-Date -Format 'yyyy-MM-dd'))

if (-not (Test-Path $logKlasor)) { New-Item -ItemType Directory -Path $logKlasor | Out-Null }

function Yaz([string]$mesaj) {
    $satir = '{0} {1}' -f (Get-Date -Format 'HH:mm:ss'), $mesaj
    Add-Content -Path $logYol -Value $satir -Encoding utf8
}

if (-not (Test-Path $proje)) {
    Yaz "HATA: toplayici projesi bulunamadi: $proje"
    exit 2
}

$env:PA_INGEST_URL = 'https://api.proteinavcisi.com.tr'

if ($KuruCalis) {
    $env:PA_KURU_CALIS = '1'
    Yaz 'Kuru calisma: gonderim yapilmayacak.'
} else {
    if (-not (Test-Path $anahtarYol)) {
        Yaz "HATA: ingest anahtari bulunamadi: $anahtarYol"
        exit 2
    }
    # Anahtar ekrana/loga DUSMUYOR, dogrudan ortam degiskenine aliniyor.
    $env:PA_INGEST_KEY = (Get-Content -Path $anahtarYol -Raw).Trim()
}

Yaz 'Toplama basliyor.'
# 2>&1 burada gerekli: toplayicinin kendi loglari stderr'e de dusebiliyor ve
# hepsini tek dosyada gormek istiyoruz. Cikis kodu ayrica kontrol ediliyor.
$cikti = & dotnet run -c Release --project $proje -- supplementler 2>&1
$kod = $LASTEXITCODE

$cikti | Where-Object { $_ -match 'Toplayici\[0\]' } | ForEach-Object { Yaz $_ }

if ($kod -ne 0) {
    Yaz "BASARISIZ (cikis kodu $kod)."
    # Son 20 satiri da yaz — hata ayiklamak icin.
    $cikti | Select-Object -Last 20 | ForEach-Object { Yaz "  $_" }
} else {
    Yaz 'Tamamlandi.'
}

# Anahtari surecin ortamindan temizle.
Remove-Item Env:\PA_INGEST_KEY -ErrorAction SilentlyContinue

# 30 gunden eski loglari sil.
Get-ChildItem $logKlasor -Filter 'supplementler-*.log' |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } |
    Remove-Item -Force -ErrorAction SilentlyContinue

exit $kod
