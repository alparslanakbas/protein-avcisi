#!/usr/bin/env bash
# Günlük veritabanı yedeği (cron'dan çalışır).
#
# Veritabanı Neon'dan VM'e taşındıktan sonra yedekleme sorumluluğu bize
# geçti — Neon bunu yönetilen hizmet olarak yapıyordu. Bu betik yedeği
# ALIP DOĞRULUYOR: doğrulanmamış yedek, yedek sayılmaz.
#
# DOĞRULAMA İKİ KADEMELİ:
#   1. `pg_restore -l` — arşivin içindekiler tablosu okunabiliyor mu.
#      Ucuz ama ZAYIF: yalnızca başlığı okur, veriyi hiç açmaz. Bozuk bir
#      dump bu testi geçip geri yüklemede patlayabilir.
#   2. GERÇEK GERİ YÜKLEME — dump ayrı bir veritabanına açılıp satır
#      sayıları kontrol ediliyor (5 Eylül'de eklendi). O güne kadar
#      yalnızca 1. kademe vardı ve "yedek doğrulanıyor" denirken aslında
#      dosyanın açılabildiği hiç denenmemişti.
#
# GÜVENLİK: geri yükleme hedefi SABİT ve canlı veritabanından farklı bir ad.
# Değişken kullanılmıyor ki yanlış bir değer canlı şemayı ezmesin.
set -euo pipefail

PROJE=/home/ubuntu/protein-avcisi
HEDEF=/home/ubuntu/pgyedek
SAKLAMA_GUN=14
# Canlı veritabanı: indirim_takip. Aşağıdaki ad ONDAN FARKLI olmak zorunda.
DOGRULAMA_DB=yedek_dogrulama

mkdir -p "$HEDEF"
cd "$PROJE"

DAMGA=$(date +%Y%m%d-%H%M)
KONTEYNER_YOL=/tmp/yedek-$DAMGA.dump
HOST_YOL="$HEDEF/indirim_takip-$DAMGA.dump"

psql_calistir() {
  docker compose exec -T db psql -U protein -d postgres -tAc "$1" </dev/null
}

dogrulama_dbsini_sil() {
  psql_calistir "DROP DATABASE IF EXISTS $DOGRULAMA_DB;" > /dev/null 2>&1 || true
}

# Betik nasıl biterse bitsin (hata dahil) geçici veritabanı arkada kalmasın.
trap dogrulama_dbsini_sil EXIT

# Dump konteyner içinde alınıyor: parola gerekmiyor (yerel soket), dolayısıyla
# sır hiçbir komut satırına ya da log'a düşmüyor.
docker compose exec -T db pg_dump -U protein -d indirim_takip \
  -Fc --no-owner --no-privileges -f "$KONTEYNER_YOL" </dev/null

# --- 1. kademe: arşiv içindekiler listelenebiliyor mu?
docker compose exec -T db pg_restore -l "$KONTEYNER_YOL" </dev/null > /dev/null

# --- 2. kademe: dump GERÇEKTEN açılıyor mu?
dogrulama_dbsini_sil
psql_calistir "CREATE DATABASE $DOGRULAMA_DB;" > /dev/null

# --no-owner/--no-privileges: dump zaten öyle alındı, restore tarafında da
# rol farkı yüzünden gereksiz hata üretmesin.
docker compose exec -T db pg_restore -U protein -d "$DOGRULAMA_DB" \
  --no-owner --no-privileges "$KONTEYNER_YOL" </dev/null > /dev/null

# Satır sayıları: dump az önce alındığı için canlıyla neredeyse birebir
# olmalı. Tam eşitlik ARANMIYOR — dump ile kontrol arasında bir tarama turu
# yazabilir; %5 tolerans o pencereyi karşılıyor ama gerçek bir veri kaybını
# (yarım açılan, boş kalan dump) yine yakalıyor.
CANLI=$(docker compose exec -T db psql -U protein -d indirim_takip -tAc \
  'SELECT COUNT(*) FROM "Products";' </dev/null | tr -d '[:space:]')
GERI=$(docker compose exec -T db psql -U protein -d "$DOGRULAMA_DB" -tAc \
  'SELECT COUNT(*) FROM "Products";' </dev/null | tr -d '[:space:]')

if [ "${GERI:-0}" -lt 1 ]; then
  echo "HATA: geri yuklenen yedekte hic urun yok (canli: $CANLI)" >&2
  exit 1
fi

FARK=$(( CANLI > GERI ? CANLI - GERI : GERI - CANLI ))
if [ "$CANLI" -gt 0 ] && [ $(( FARK * 100 / CANLI )) -gt 5 ]; then
  echo "HATA: geri yukleme satir sayisi tutmuyor (canli: $CANLI, geri: $GERI)" >&2
  exit 1
fi

dogrulama_dbsini_sil

docker compose cp "db:$KONTEYNER_YOL" "$HOST_YOL" </dev/null
docker compose exec -T db rm -f "$KONTEYNER_YOL" </dev/null

# Boyut da bir sağlık sinyali: aniden küçülen bir yedek sessiz veri kaybına işaret eder.
BOYUT=$(stat -c %s "$HOST_YOL")
if [ "$BOYUT" -lt 200000 ]; then
  echo "UYARI: yedek beklenenden kucuk ($BOYUT bayt): $HOST_YOL" >&2
  exit 1
fi

find "$HEDEF" -name 'indirim_takip-*.dump' -mtime +$SAKLAMA_GUN -delete
echo "$(date '+%Y-%m-%d %H:%M') yedek tamam: $HOST_YOL ($BOYUT bayt, geri yukleme dogrulandi: $GERI urun)"
