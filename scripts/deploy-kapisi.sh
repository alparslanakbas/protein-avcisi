#!/usr/bin/env bash
# DEPLOY KAPISI — GitHub'ın deploy anahtarlarının sunucuda çalıştırabildiği TEK
# program (27 Eylül, güvenlik incelemesi).
#
# NEDEN: deploy sırrı, sunucudaki tek yetkili anahtardı, yani yöneticinin kendi
# anahtarıydı ve ubuntu kullanıcısı docker grubunda (fiilen root). GitHub'daki
# sır sızsaydı sunucu tümüyle ele geçerdi. Artık her deponun AYRI anahtarı var
# ve authorized_keys onu bu betiğe bağlıyor:
#   restrict,command="/usr/local/bin/deploy-kapisi tr" ssh-ed25519 ... github-deploy-tr
#   restrict,command="/usr/local/bin/deploy-kapisi wheyproof" ssh-ed25519 ... github-deploy-wheyproof
# Sızan bir deploy anahtarı yalnızca "GitHub'daki kodu çek ve deploy et" diyebilir;
# rastgele komut, kabuk, port yönlendirme yok (restrict). Push yetkisi zaten kod
# çalıştırma yetkisi demek, yani bu kapı bir yetki eklemiyor, yalnızca kısıyor.
#
# Adımların kendisi depoda (scripts/deploy.sh): sürüm kontrolünde kalsın diye.
# Bu dosya ise sunucuda /usr/local/bin/deploy-kapisi olarak ELLE kurulu (slice
# dosyaları gibi); değişirse: sudo install -m 755 scripts/deploy-kapisi.sh /usr/local/bin/deploy-kapisi
#
# İstenen komut (SSH_ORIGINAL_COMMAND) yalnızca iki değerden biri olabilir:
#   deploy — depoyu günceller ve scripts/deploy.sh'ı çalıştırır
#   durum  — o projenin konteynerlerini listeler (zararsız; bağlantıyı sınamak için)
# Gerisi reddediliyor ve syslog'a yazılıyor (journalctl -t deploy-kapisi).
set -euo pipefail

case "${1:-}" in
  tr) dizin=/home/ubuntu/protein-avcisi; proje=protein-avcisi ;;
  wheyproof) dizin=/home/ubuntu/wheyproof; proje=wheyproof ;;
  *) echo "deploy-kapisi: bilinmeyen proje" >&2; exit 2 ;;
esac

istek=${SSH_ORIGINAL_COMMAND:-}
istek=${istek//$'\r'/}
# Baştaki ve sondaki boşlukları at (iş akışı betiği sonuna satır sonu ekleyebilir).
istek="${istek#"${istek%%[![:space:]]*}"}"
istek="${istek%"${istek##*[![:space:]]}"}"

case "$istek" in
  deploy)
    logger -t deploy-kapisi "$1: deploy basladi"
    cd "$dizin"
    # --ff-only: geçmiş ayrışmışsa sessizce merge commit üretmek yerine
    # yüksek sesle başarısız olsun.
    git pull --ff-only
    echo "Deploy edilen commit: $(git rev-parse --short HEAD)"
    exec bash scripts/deploy.sh
    ;;
  durum)
    exec docker ps --filter "label=com.docker.compose.project=$proje" --format '{{.Names}} | {{.Status}}'
    ;;
  *)
    logger -t deploy-kapisi "$1: REDDEDILDI: ${istek:0:120}"
    echo "deploy-kapisi: izin verilmeyen komut" >&2
    exit 1
    ;;
esac
