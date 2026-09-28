#!/usr/bin/env bash
# Sunucudaki deploy adımları. GitHub Actions bunu DOĞRUDAN çalıştırmıyor:
# deploy anahtarı sunucuda yalnızca deploy kapısını (scripts/deploy-kapisi.sh)
# çalıştırabiliyor; kapı depoyu güncelleyip (git pull) bu betiği güncel
# commit'ten çalıştırıyor. Adımlar 27 Eylül'e kadar iş akışının içinde
# yazılıydı, birebir buraya taşındı.
set -e
cd "$(dirname "$0")/.."

docker compose build

# --force-recreate ZORUNLU: "docker compose up -d" tek başına,
# imaj yeniden derlenmiş olsa bile container'ı değiştirmeyebiliyor.
# Bu gerçekten yaşandı — backend imajı derlendi, container
# dokunulmadan kaldı ve canlı ESKİ kodu servis etmeye devam etti,
# üstelik workflow "başarılı" göründü. Sessiz başarısızlık, açık
# başarısızlıktan çok daha pahalı; birkaç saniyelik kesinti buna
# değer.
#
# --no-deps: Caddy'ye dokunma. Onun yeniden oluşturulması TLS
# bağlantılarını gereksiz yere koparırdı; yapılandırması zaten
# aşağıda kesintisiz yeniden yükleniyor.
docker compose up -d --force-recreate --no-deps backend frontend

docker compose up -d caddy
# Caddyfile dizin olarak bağlandığı için dosya güncel, ama Caddy
# bellekteki yapılandırmayla çalışmaya devam ediyor; reload
# kesintisiz devralmasını sağlıyor.
docker compose exec -T caddy caddy reload --config /etc/caddy/Caddyfile --adapter caddyfile

# Deploy'un GERÇEKTEN çalıştığını kanıtla. Bu adım olmadan
# workflow, canlıda eski kod dururken bile yeşil görünüyordu.
# Servislere Caddy container'ı üzerinden, compose ağının içinden
# istek atıyoruz — dışarıdan bakınca Cloudflare önbelleği araya
# girebilirdi.
echo "Servislerin ayağa kalkması bekleniyor..."
saglikli=
for i in $(seq 1 36); do
  # Frontend'e Host başlığı ŞART: Angular SSR'ın host koruması
  # (NG_ALLOWED_HOSTS) tanımadığı bir Host'a 400 dönüyor, yani
  # başlıksız prob sağlıklı bir sunucuyu bile hatalı gösterirdi.
  if docker compose exec -T caddy wget -q -O /dev/null --timeout=5 http://backend:8080/api/stats \
     && docker compose exec -T caddy wget -q -O /dev/null --timeout=5 \
        --header="Host: www.proteinavcisi.com.tr" http://frontend:4000/; then
    echo "Doğrulama tamam: backend ve frontend yanıt veriyor."
    saglikli=1
    break
  fi
  sleep 5
done

if [ -z "$saglikli" ]; then
  echo "HATA: servisler 3 dakika içinde yanıt vermedi."
  docker compose ps
  docker compose logs --tail 60 backend frontend
  exit 1
fi

# DIŞARIDAN TOPLAYICI (Supplementler, Renovafood), 28 Eylül'den beri deploy'un
# parçası. Konteyner değil: /opt/toplayici'de duran tek dosyalık bir ikili, cron
# onu `toplayici` kullanıcısı olarak çalıştırıyor (WireGuard tüneli yalnızca o
# kullanıcının trafiğini ev bağlantısına çıkarıyor). Deploy onu güncellemiyordu
# ve ikili 4 Eylül'den 28 Eylül'e kadar eski kaldı: aradaki süzgeç düzeltmeleri
# Supplementler'e hiç uygulanmadı, kimse de fark etmedi.
#
# Yalnızca toplayıcının derlendiği projeler ya da bu adımın kendi dosyaları
# değiştiyse derleniyor; kurulu ikilinin commit'i /opt/toplayici/SURUM'da.
# Site bu adımdan ÖNCE doğrulandı: buradaki bir hata siteyi etkilemez, ama
# deploy'u kırmızı gösterir, ki toplayıcı bir daha sessizce eski kalmasın.
toplayiciyi_guncelle() {
  local dizin=/opt/toplayici
  local yollar=(
    backend/src/IndirimTakip.Toplayici
    backend/src/IndirimTakip.Infrastructure
    backend/src/IndirimTakip.Core
    backend/Dockerfile.toplayici
    scripts/toplayici-calistir.sh
  )
  local kurulu simdiki
  kurulu=$(sudo -n cat "$dizin/SURUM" 2>/dev/null || true)
  simdiki=$(git rev-parse HEAD)

  if [ -n "$kurulu" ] && git cat-file -e "$kurulu^{commit}" 2>/dev/null \
     && git diff --quiet "$kurulu" "$simdiki" -- "${yollar[@]}"; then
    echo "Toplayıcı güncel (${kurulu:0:7}), derlenmedi."
    return 0
  fi

  echo "Toplayıcı derleniyor (kurulu: ${kurulu:-yok}, yeni: $simdiki)..."
  cikti=$(mktemp -d)
  trap 'rm -rf "$cikti"' EXIT
  docker build -f backend/Dockerfile.toplayici --output "type=local,dest=$cikti" backend

  # Önce yanına kurulup sınanıyor: bilinmeyen bir kaynak adıyla "Bilinmeyen
  # kaynak" deyip 2 ile çıkmalı. Ağa çıkmıyor; ikilinin bu makinede, bu
  # kullanıcıyla ayağa kalktığını kanıtlıyor. Geçemezse eskisi yerinde kalıyor.
  local yeni="$dizin/.IndirimTakip.Toplayici.yeni"
  sudo -n install -o root -g root -m 755 "$cikti/IndirimTakip.Toplayici" "$yeni"
  local sinama sinama_kodu=0
  sinama=$(sudo -n -u toplayici "$yeni" deploy-sinamasi 2>&1) || sinama_kodu=$?
  if [ "$sinama_kodu" -ne 2 ] || ! grep -q "Bilinmeyen kaynak" <<< "$sinama"; then
    sudo -n rm -f "$yeni"
    echo "HATA: yeni toplayıcı sınamayı geçemedi (çıkış $sinama_kodu); ESKİSİ yerinde bırakıldı."
    echo "$sinama" | tail -5
    return 1
  fi

  # Yeniden adlandırma atomik: tam o anda cron'la çalışan bir tur eski dosyayı
  # (açık kalan inode'u) kullanmaya devam ediyor. Bir önceki ikili geri dönüş
  # için saklanıyor.
  if sudo -n test -f "$dizin/IndirimTakip.Toplayici"; then
    sudo -n cp -p "$dizin/IndirimTakip.Toplayici" "$dizin/IndirimTakip.Toplayici.onceki"
  fi
  sudo -n mv -f "$yeni" "$dizin/IndirimTakip.Toplayici"
  sudo -n install -o root -g root -m 755 scripts/toplayici-calistir.sh "$dizin/.calistir.sh.yeni"
  sudo -n mv -f "$dizin/.calistir.sh.yeni" "$dizin/calistir.sh"
  echo "$simdiki" | sudo -n tee "$dizin/SURUM" > /dev/null
  echo "Toplayıcı kuruldu: ${simdiki:0:7}."
}

toplayiciyi_guncelle
