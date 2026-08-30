#!/usr/bin/env bash
# Günlük veritabanı yedeği (cron'dan çalışır).
#
# Veritabanı Neon'dan VM'e taşındıktan sonra yedekleme sorumluluğu bize
# geçti — Neon bunu yönetilen hizmet olarak yapıyordu. Bu betik yedeği
# ALIP DOĞRULUYOR: doğrulanmamış yedek, yedek sayılmaz. Bozuk/boş bir
# dump sessizce birikirse felaket anında fark edilir, o yüzden her tur
# pg_restore ile okunabilirlik kontrol ediliyor.
set -euo pipefail

PROJE=/home/ubuntu/protein-avcisi
HEDEF=/home/ubuntu/pgyedek
SAKLAMA_GUN=14

mkdir -p "$HEDEF"
cd "$PROJE"

DAMGA=$(date +%Y%m%d-%H%M)
KONTEYNER_YOL=/tmp/yedek-$DAMGA.dump
HOST_YOL="$HEDEF/indirim_takip-$DAMGA.dump"

# Dump konteyner içinde alınıyor: parola gerekmiyor (yerel soket), dolayısıyla
# sır hiçbir komut satırına ya da log'a düşmüyor.
docker compose exec -T db pg_dump -U protein -d indirim_takip \
  -Fc --no-owner --no-privileges -f "$KONTEYNER_YOL" </dev/null

# Doğrulama: arşiv içindekiler listelenebiliyor mu?
docker compose exec -T db pg_restore -l "$KONTEYNER_YOL" </dev/null > /dev/null

docker compose cp "db:$KONTEYNER_YOL" "$HOST_YOL" </dev/null
docker compose exec -T db rm -f "$KONTEYNER_YOL" </dev/null

# Boyut da bir sağlık sinyali: aniden küçülen bir yedek sessiz veri kaybına işaret eder.
BOYUT=$(stat -c %s "$HOST_YOL")
if [ "$BOYUT" -lt 200000 ]; then
  echo "UYARI: yedek beklenenden kucuk ($BOYUT bayt): $HOST_YOL" >&2
  exit 1
fi

find "$HEDEF" -name 'indirim_takip-*.dump' -mtime +$SAKLAMA_GUN -delete
echo "$(date '+%Y-%m-%d %H:%M') yedek tamam: $HOST_YOL ($BOYUT bayt)"
