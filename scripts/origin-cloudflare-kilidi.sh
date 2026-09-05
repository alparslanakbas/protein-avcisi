#!/usr/bin/env bash
# Origin kilidi: 80/443 yalnızca Cloudflare IP aralıklarından kabul edilir.
#
# NEDEN VAR (5 Eylül'de ölçüldü): sunucunun genel IP'si (89.168.93.197)
# doğrudan istek kabul ediyordu. `curl --resolve www.proteinavcisi.com.tr:443:
# 89.168.93.197` hem siteyi hem /api/stats'ı 200 ile döndürdü ve yanıtta
# `cf-ray` başlığı YOKTU — yani Cloudflare yolun dışındaydı. Bu, Cloudflare'in
# DDoS koruması, bot denetimi ve hız sınırı kurallarının tamamının
# atlanabilmesi demek.
#
# NE ATLANMIYOR (ölçüldü): uygulamanın kendi savunmaları çalışıyor. Doğrudan
# origin'e 12 hızlı POST atıldı, 11. istekte 429 geldi; /api/dev/click-report
# doğrudan origin'den 401 döndü. Yani kimlik doğrulama ve ASP.NET hız sınırı
# Cloudflare'e bağlı DEĞİL. Kilit, Cloudflare katmanındaki korumayı geri
# kazanmak için.
#
# NEDEN DOCKER-USER, INPUT DEĞİL — BU BİR TUZAK: 80/443 trafiği INPUT
# zincirine HİÇ uğramıyor. Docker onu nat/PREROUTING'de doğrudan Caddy
# konteynerine DNAT ediyor (172.18.0.4) ve paket FORWARD -> DOCKER-USER
# yolundan geçiyor. INPUT'taki mevcut "dport 80/443 ACCEPT" kuralları
# pratikte ÖLÜ; kural oraya yazılsaydı hiçbir şey değişmez ama iş
# yapılmış sanılırdı.
#
# SERTİFİKA YENİLEMESİ BOZULMUYOR (ölçüldü): DNS proxy'li olduğu için
# Let's Encrypt origin IP'sine hiç bağlanmıyor, doğrulama Cloudflare
# üzerinden geliyor. Canlıda kanıtlandı — /.well-known/acme-challenge/
# yoluna atılan test isteği Caddy loguna `remote_addr: 172.71.183.4`
# (bir Cloudflare adresi) olarak düştü.
#
# SSH (22) ve WireGuard (51820/udp) bu betiğe HİÇ dokunmuyor.
set -euo pipefail

DIS=enp0s6
YEDEK=/root/cf-kilit-yedek.v4
ONAY=/run/cf-kilit-onaylandi
GERIALMA_SN=420

iptables-save > "$YEDEK"
rm -f "$ONAY"

# SİGORTA: kural konduktan sonra onay dosyası oluşturulmazsa sunucu kendini
# eski hâline döndürür. Bir hata siteyi kapatırsa 7 dakikada kendiliğinden
# açılır; elle müdahaleye bağlı kalmıyoruz.
setsid bash -c "sleep $GERIALMA_SN; if [ ! -f $ONAY ]; then iptables-restore < $YEDEK; logger -t cf-kilit 'onay gelmedi, geri alindi'; fi" </dev/null >/dev/null 2>&1 &

curl -fsS https://www.cloudflare.com/ips-v4 | tr -d '\r' | grep -E '^[0-9]' > /run/cf4.txt
ADET=$(wc -l < /run/cf4.txt)
# Liste beklenmedik biçimde kısa gelirse (ağ hatası, biçim değişikliği)
# yarım listeyle kilitlemek siteyi kapatırdı.
if [ "$ADET" -lt 10 ]; then
  echo "HATA: Cloudflare listesi kisa ($ADET satir), iptal edildi" >&2
  exit 1
fi

iptables -F DOCKER-USER
while read -r cidr; do
  iptables -A DOCKER-USER -i "$DIS" -s "$cidr" -p tcp -m multiport --dports 80,443 -j RETURN
done < /run/cf4.txt
# REJECT değil DROP: origin taramalara yanıt bile vermesin.
iptables -A DOCKER-USER -i "$DIS" -p tcp -m multiport --dports 80,443 \
  -m comment --comment "Cloudflare disi origin erisimi" -j DROP

echo "$ADET Cloudflare araligi izinli, digerleri DROP."
echo "ONAY VERILMEZSE ${GERIALMA_SN} sn sonra otomatik geri alinir:"
echo "  sudo touch $ONAY && sudo netfilter-persistent save"
