#!/usr/bin/env bash
# Günlük mağaza tıklaması serisi (cron'dan, yedekten hemen sonra çalışır).
#
# NEDEN VAR: `Product.ClickCount` KÜMÜLATİF ve tarihsiz — site açılışından
# beri toplam. "SEO değişikliğinden sonra tıklama arttı mı" sorusu bu
# sayaçla cevaplanamıyordu. Tek tek tıklama günlüğü tutmak (yeni tablo,
# yazma yolunda değişiklik) bu soru için fazla; günde bir kez toplamı
# yazmak yetiyor ve yazma yoluna hiç dokunmuyor.
#
# GEÇMİŞ YEDEKLERDEN GERİ ÇIKARILDI: her günlük dump o günkü ClickCount'u
# taşıdığı için 30 Ağustos'a kadar olan seri sonradan üretilip bu dosyaya
# tohumlandı (5 Eylül). Yani seri, kurulduğu günden değil, yedeklerin
# başladığı günden başlıyor.
#
# Dosya BİLEREK yedek rotasyonunun dışında: dump'lar 14 günde siliniyor,
# bu seri ise birikmeye devam etmeli.
set -euo pipefail

PROJE=/home/ubuntu/protein-avcisi
SERI=/home/ubuntu/pgyedek/tiklama-serisi.csv

cd "$PROJE"

BUGUN=$(date +%Y-%m-%d)

# Aynı gün iki kez çalışırsa satır tekrarlanmasın (cron + elle çalıştırma).
if [ -f "$SERI" ] && grep -q "^$BUGUN," "$SERI"; then
  echo "$BUGUN zaten kayıtlı, atlandı."
  exit 0
fi

if [ ! -f "$SERI" ]; then
  echo "tarih,toplam_tiklama,urun_sayisi" > "$SERI"
fi

OKU=$(docker compose exec -T db psql -U protein -d indirim_takip -tAc \
  'SELECT COALESCE(SUM("ClickCount"),0) || '"'"','"'"' || COUNT(*) FROM "Products";' </dev/null | tr -d '[:space:]')

if [ -z "$OKU" ]; then
  echo "HATA: tiklama sayisi okunamadi" >&2
  exit 1
fi

echo "$BUGUN,$OKU" >> "$SERI"
echo "$BUGUN kaydedildi: $OKU"
