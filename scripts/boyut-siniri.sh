#!/usr/bin/env bash
# Kaynak dosya boyut sınırı (güvenlik incelemesi B5, 26 Eylül).
#
# DealsQueryService (1.132 satır) ve AdminEndpoints bölündü ama tekrar
# büyümelerini engelleyen bir şey yoktu; deals-list.ts bölünmeden sonra da
# 1102'den 1107'ye çıktı. Bu betik deploy kapısında çalışıyor:
#   - Yeni ya da listede olmayan dosya SINIR satırı geçemez.
#   - Bugün sınırı aşan dosyalar bugünkü boylarında DONDURULDU (TAVAN):
#     büyüyemezler. Büyütmek gerekiyorsa önce bölünür; ya da tavan bu
#     dosyada bilerek yükseltilir ve commit'te görünür.
#   - Tavanlı dosya küçülünce betik haber veriyor: tavan da düşürülsün ki
#     kazanılan yer geri harcanmasın.
# CSS kapsam dışı: bileşen stillerinde Angular'ın bayt bütçesi zaten var.
# Migration'lar EF'in ürettiği dosyalar, testler (.spec.ts) de sayılmıyor.
set -euo pipefail
cd "$(dirname "$0")/.."

SINIR=800
declare -A TAVAN=(
  [frontend/src/app/yonetim-page/yonetim-page.html]=1590
  [frontend/src/app/deals-list/deals-list.ts]=1107
  [frontend/src/app/yonetim-page/yonetim-page.ts]=1013
  [backend/src/IndirimTakip.Infrastructure/Deals/DealsQueryService.cs]=860
)

# Liste ÖNCE değişkene alınıyor: süreç ikamesinin (< <(...)) çıkış kodunu
# set -e görmüyor; git hata verince döngü sıfır dosya okuyup "tamam" diyordu
# (ölçüldü, 27 Eylül). Buradaki atamada git hatası betiği düşürüyor; liste
# başka bir sebeple boş gelirse aşağıdaki sayaç düşürüyor.
liste=$(git ls-files 'backend/src/*.cs' 'frontend/src/*.ts' 'frontend/src/*.html')
hata=0
sayac=0
while IFS= read -r dosya; do
  [[ -z $dosya || $dosya == */Migrations/* || $dosya == *.spec.ts ]] && continue
  sayac=$((sayac + 1))
  satir=$(wc -l < "$dosya")
  tavan=${TAVAN[$dosya]:-$SINIR}
  if (( satir > tavan )); then
    echo "SINIR AŞILDI: $dosya $satir satır (izin verilen $tavan)"
    hata=1
  fi
done <<< "$liste"

if (( sayac == 0 )); then
  echo "HATA: hiç dosya okunmadı — betik git deposunun içinde mi çalışıyor?"
  exit 1
fi

for dosya in "${!TAVAN[@]}"; do
  if [[ ! -f $dosya ]]; then
    echo "NOT: $dosya artık yok, TAVAN listesinden çıkarın."
  elif (( $(wc -l < "$dosya") < TAVAN[$dosya] )); then
    echo "NOT: $dosya $(wc -l < "$dosya") satıra indi; tavanını da düşürün (şu an ${TAVAN[$dosya]})."
  fi
done

if (( hata )); then
  echo "Dosyayı bölün ya da (bilerek) scripts/boyut-siniri.sh içindeki tavanı yükseltin."
  exit 1
fi
echo "Boyut sınırı tamam ($sayac dosya; sınır $SINIR satır, ${#TAVAN[@]} dosya bugünkü boyunda dondurulmuş)."
