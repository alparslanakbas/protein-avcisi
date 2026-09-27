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
for i in $(seq 1 36); do
  # Frontend'e Host başlığı ŞART: Angular SSR'ın host koruması
  # (NG_ALLOWED_HOSTS) tanımadığı bir Host'a 400 dönüyor, yani
  # başlıksız prob sağlıklı bir sunucuyu bile hatalı gösterirdi.
  if docker compose exec -T caddy wget -q -O /dev/null --timeout=5 http://backend:8080/api/stats \
     && docker compose exec -T caddy wget -q -O /dev/null --timeout=5 \
        --header="Host: www.proteinavcisi.com.tr" http://frontend:4000/; then
    echo "Doğrulama tamam: backend ve frontend yanıt veriyor."
    exit 0
  fi
  sleep 5
done

echo "HATA: servisler 3 dakika içinde yanıt vermedi."
docker compose ps
docker compose logs --tail 60 backend frontend
exit 1
