#!/usr/bin/env bash
# Besin değeri kapsamının günlük serisi + sessiz arıza alarmı.
#
# NEDEN VAR: detay tamamlama (ProductDetailBackfillService) 2 günde bir
# çalışıyor ve SESSİZCE bozulabilir. Bir kaynak sayfa yapısını değiştirirse
# istekler başarılı döner, ürünlere "bakıldı" damgası atılır ama besin
# değeri gelmez — üstelik damga atıldığı için o ürünler BİR DAHA
# DENENMEZ. Log kimse bakmadığı için de fark edilmez. 5 Eylül'de tam bu
# aileden iki hata yaşandı (bayi adresleri çekiliyordu; sıralama olmadığı
# için aynı sayfalar tekrar iniyordu) ve ikisi de yalnızca canlı veriye
# bakınca görüldü.
#
# GÜNLÜK yazılıyor ama tamamlama 2 günde bir çalışıyor: ara günlerde satır
# değişmez, bu normaldir. Günlük örnekleme bilerek — 2 günde bir örnekleseydi
# faz kayması yüzünden bir turu tamamen ıskalayabilirdik.
#
# Dosya BİLEREK yedek rotasyonunun dışında: dump'lar 14 günde siliniyor,
# bu seri birikmeye devam etmeli.
set -euo pipefail

PROJE=/home/ubuntu/protein-avcisi
SERI=/home/ubuntu/pgyedek/besin-serisi.csv

# Bir markanın çekicisinin bozulduğuna karar vermek için: bu kadar yeni ürüne
# bakılmış olmasına rağmen tek bir besin değeri bile gelmemişse.
# Eşik 5 — 1-2 üründe besin çıkmaması normal (aksesuar, çoklu paket).
ESIK_BAKILAN=5

cd "$PROJE"

BUGUN=$(date +%Y-%m-%d)

if [ -f "$SERI" ] && grep -q "^$BUGUN," "$SERI"; then
  echo "$BUGUN zaten kayıtlı, atlandı."
  exit 0
fi

if [ ! -f "$SERI" ]; then
  echo "tarih,marka,kendi_urunu,besin,bakildi" > "$SERI"
fi

# Yalnızca markanın KENDİ sitesinden gelen ürünler: besin değeri sadece
# onlarda mümkün (bayi sayfalarından çekilmiyor).
#
# "Bakılmış olma" koşulu WHERE'de DEĞİL HAVING'de: WHERE'de olduğunda
# COUNT(*) da yalnızca bakılanları sayıyor ve "kendi_urunu" sütunu
# "bakildi" ile aynı sayıyı tekrarlıyordu (ilk sürümde öyleydi, çıktı
# okunurken görüldü — Fellas 118 ürünlükken 82 yazıyordu). Toplam ürün
# sayısı ilerlemenin paydası, doğru olmak zorunda.
#
# HIQ bu seride YOK ve bu doğru: onun besin değeri normal taramadan
# geliyor, detay tamamlama ona hiç dokunmuyor. Seri bu işin izlenmesi için.
OKU=$(docker compose exec -T db psql -U protein -d indirim_takip -tAF, -c "
SELECT b.\"Name\",
       COUNT(*),
       COUNT(p.\"NutritionJson\"),
       COUNT(p.\"NutritionCheckedAt\")
FROM \"Products\" p JOIN \"Brands\" b ON b.\"Id\" = p.\"BrandId\"
WHERE p.\"Seller\" IS NULL
GROUP BY b.\"Name\"
HAVING COUNT(p.\"NutritionCheckedAt\") > 0
ORDER BY b.\"Name\";" </dev/null | sed '/^$/d')

if [ -z "$OKU" ]; then
  echo "HATA: besin kapsami okunamadi" >&2
  exit 1
fi

# --- Alarm: dünkü satırla karşılaştır.
# Kural KENDİ KENDİNİ AYARLIYOR, marka listesi tutmuyoruz: "bakılan arttı ama
# besin hiç artmadı" bozulmanın imzası. İşi bitmiş markalarda (ProteinOcean
# gibi, kaynağı besin yayınlamıyor) bakılan da artmadığı için alarm üretmez —
# beyaz liste tutmak gerekmiyor, o liste zamanla çürürdü.
UYARI=""
while IFS=, read -r marka urun besin bakildi; do
  ONCEKI=$(grep ",$marka," "$SERI" 2>/dev/null | tail -1 || true)
  [ -z "$ONCEKI" ] && continue

  ONCEKI_BESIN=$(echo "$ONCEKI" | cut -d, -f4)
  ONCEKI_BAKILAN=$(echo "$ONCEKI" | cut -d, -f5)

  YENI_BAKILAN=$(( bakildi - ONCEKI_BAKILAN ))
  YENI_BESIN=$(( besin - ONCEKI_BESIN ))

  if [ "$YENI_BAKILAN" -ge "$ESIK_BAKILAN" ] && [ "$YENI_BESIN" -le 0 ]; then
    UYARI="$UYARI  $marka: $YENI_BAKILAN yeni ürüne bakıldı, besin değeri 0 arttı"$'\n'
  fi
done <<< "$OKU"

while IFS= read -r satir; do
  echo "$BUGUN,$satir" >> "$SERI"
done <<< "$OKU"

TOPLAM=$(echo "$OKU" | awk -F, '{b+=$3; k+=$4} END {print b"/"k}')
echo "$BUGUN besin serisi yazıldı (besin/bakılan: $TOPLAM)"

if [ -n "$UYARI" ]; then
  echo "UYARI - besin çekicisi bozulmuş olabilir:" >&2
  printf '%s' "$UYARI" >&2
fi
