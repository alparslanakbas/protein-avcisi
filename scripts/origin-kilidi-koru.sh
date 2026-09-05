#!/usr/bin/env bash
# Origin kilidini AYAKTA TUTAN bekçi. Günde bir çalışır (cron).
#
# NEDEN GEREKLİ — kilit iki şekilde SESSİZCE kaybolabilir:
#   1. Docker daemon yeniden başlarsa DOCKER-USER zincirini yeniden kurar.
#   2. Cloudflare IP aralıkları zamanla değişir; listeye yeni bir aralık
#      eklendiğinde o aralıktan gelen GERÇEK ziyaretçiler DROP edilir —
#      yani eskimiş liste siteyi kısmen kapatır.
# İkisinde de hata görünmez: birinde koruma yok olur ama site çalışır,
# diğerinde site bazı ziyaretçilere kapanır ama loglarda bir şey çıkmaz.
#
# Betik idempotent: her turda kuralları listeden yeniden kuruyor.
# `origin-cloudflare-kilidi.sh`ten farkı, geri alma sigortası OLMAMASI —
# cron'da çalıştığı için onaylayacak kimse yok.
set -euo pipefail

DIS=enp0s6
LISTE=/run/cf4-koru.txt

# Liste çekilemezse MEVCUT KURALLARA DOKUNMA. Yarım/boş listeyle zinciri
# yeniden kurmak siteyi kapatırdı; eski kuralla devam etmek her zaman daha
# güvenli.
if ! curl -fsS --max-time 20 https://www.cloudflare.com/ips-v4 | tr -d '\r' | grep -E '^[0-9]' > "$LISTE"; then
  echo "UYARI: Cloudflare listesi alinamadi, kurallara dokunulmadi" >&2
  exit 0
fi

ADET=$(wc -l < "$LISTE")
if [ "$ADET" -lt 10 ]; then
  echo "UYARI: Cloudflare listesi kisa ($ADET satir), kurallara dokunulmadi" >&2
  exit 0
fi

# Zaten doğru mu? Öyleyse hiç dokunma (gereksiz yazma = gereksiz risk).
MEVCUT=$(iptables -S DOCKER-USER 2>/dev/null | grep -c "multiport --dports 80,443" || true)
BEKLENEN=$(( ADET + 1 ))
if [ "$MEVCUT" -eq "$BEKLENEN" ]; then
  exit 0
fi

echo "origin kilidi eksik/eskimis (kural $MEVCUT, beklenen $BEKLENEN) - yeniden kuruluyor"

iptables -F DOCKER-USER
while read -r cidr; do
  iptables -A DOCKER-USER -i "$DIS" -s "$cidr" -p tcp -m multiport --dports 80,443 -j RETURN
done < "$LISTE"
iptables -A DOCKER-USER -i "$DIS" -p tcp -m multiport --dports 80,443 \
  -m comment --comment "Cloudflare disi origin erisimi" -j DROP

netfilter-persistent save >/dev/null
echo "origin kilidi yeniden kuruldu: $ADET aralik izinli"
