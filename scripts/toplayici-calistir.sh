#!/bin/sh
# Dışarıdan toplanan kaynaklar — sunucuda, WireGuard tüneli üzerinden.
#
# Kullanım: calistir.sh <kaynak>     (supplementler | renovafood)
#
# İki kaynak da aynı sebeple burada: sunucunun datacenter aralığından
# engelleniyorlar (Supplementler'de Cloudflare managed challenge, Renovafood'da
# düz 403), ev bağlantısından açılıyorlar.
#
# Bu betik "toplayici" kullanıcısı (uid 999) olarak çalışıyor ve bu ÖNEMLİ:
# yönlendirme kuralı (ip rule uidrange 999-999) yalnızca o kullanıcının
# trafiğini tünele sokuyor. Başka bir kullanıcıyla çalıştırılırsa istekler
# sunucunun kendi IP'sinden çıkar ve engele takılır.

set -u

KAYNAK="${1:-}"
if [ -z "$KAYNAK" ]; then
    echo "Kullanim: $0 <kaynak>   (supplementler | renovafood)" >&2
    exit 2
fi

LOG_DIZIN=/var/log/toplayici
LOG="$LOG_DIZIN/$KAYNAK-$(date +%Y-%m-%d).log"
VM_IP=89.168.93.197

yaz() { printf '%s %s\n' "$(date +%H:%M:%S)" "$1" >> "$LOG"; }

mkdir -p "$LOG_DIZIN" 2>/dev/null

if [ "$(id -u)" != "999" ]; then
    yaz "HATA: betik uid 999 (toplayici) ile çalışmalı, şu an $(id -u)."
    exit 2
fi

# --- ÇIKIŞ IP KONTROLÜ ----------------------------------------------------
# Tünel düşerse istekler sessizce sunucunun kendi IP'sinden çıkmaya başlar,
# engele takılır ve tur boşa gider. Bunu her turda ÖNDEN ölçüyoruz: çıkış
# IP'si sunucunun IP'siyse tünel çalışmıyordur, hiç başlamıyoruz.
#
# Ölçüm tutmazsa hemen vazgeçmiyoruz: ev bağlantısı ara ara kopup yeni IP'yle
# geri geliyor (7-8 Kasım 2026: 31 saatte üç IP). Tek denemelik ölçüm bir
# kopmada turu 12 saat erteliyordu; Renovafood'un iki turu art arda böyle
# atlandı ve sağlık ucu 503'e düştü. 7 Kasım akşamki kesinti 2,5 ile 5,5 saat
# arası sürdü, o yüzden 10 dakikada bir, en çok 6 saat yeniden deniyoruz: aynı
# kaynağın sıradaki turu 12 saat sonra, çakışmıyor. Denemeler yalnızca ipify'a
# gidiyor, kaynağa istek atılmıyor. Her deneme günlüğe yazılıyor: kesintilerin
# ne kadar sürdüğü de böylece görülüyor.
EN_COK=37   # ilk ölçüm + 36 yeniden deneme = 6 saat
BEKLE=600   # saniye
deneme=1
while :; do
    CIKIS=$(curl -s --max-time 20 https://api.ipify.org 2>/dev/null)
    if [ -z "$CIKIS" ]; then
        SORUN="çıkış IP'si öğrenilemedi (tünel muhtemelen kapalı)"
    elif [ "$CIKIS" = "$VM_IP" ]; then
        SORUN="çıkış IP'si sunucunun kendisi ($CIKIS) — tünel devrede DEĞİL"
    else
        break
    fi
    if [ "$deneme" -ge "$EN_COK" ]; then
        yaz "HATA: $SORUN; $deneme denemede düzelmedi. Tur atlandı."
        exit 1
    fi
    yaz "Deneme $deneme/$EN_COK: $SORUN. $((BEKLE / 60)) dk sonra yeniden denenecek."
    sleep "$BEKLE"
    deneme=$((deneme + 1))
done

if [ "$deneme" -gt 1 ]; then
    yaz "Tünel $deneme. denemede açıldı."
fi
yaz "Tünel çalışıyor, çıkış IP'si ev bağlantısı ($CIKIS). Toplama başlıyor: $KAYNAK."

# --- TOPLAMA --------------------------------------------------------------
PA_INGEST_URL="http://127.0.0.1:8080"
PA_INGEST_KEY=$(cat /etc/toplayici/ingest.key 2>/dev/null | tr -d '\n')
export PA_INGEST_URL PA_INGEST_KEY

if [ -z "$PA_INGEST_KEY" ]; then
    yaz "HATA: ingest anahtarı okunamadı (/etc/toplayici/ingest.key)."
    exit 2
fi

CIKTI=$(/opt/toplayici/IndirimTakip.Toplayici "$KAYNAK" 2>&1)
KOD=$?

echo "$CIKTI" | grep 'Toplayici\[0\]' | while IFS= read -r satir; do yaz "$satir"; done

if [ "$KOD" -ne 0 ]; then
    yaz "BAŞARISIZ (çıkış kodu $KOD). Son satırlar:"
    echo "$CIKTI" | tail -20 | while IFS= read -r satir; do yaz "  $satir"; done
else
    yaz "Tamamlandı."
fi

# 30 günden eski logları sil.
find "$LOG_DIZIN" -name '*.log' -mtime +30 -delete 2>/dev/null

exit "$KOD"
