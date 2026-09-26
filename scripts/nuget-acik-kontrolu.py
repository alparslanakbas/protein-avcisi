"""NuGet paketlerinde bilinen açık kontrolü (haftalık iş; güvenlik incelemesi, 26 Eylül).

`dotnet list package --vulnerable` açık bulsa da 0 dönüyor; bu yüzden çıktı
JSON olarak okunup karar burada veriliyor. Açık veritabanına ulaşılamazsa da
başarısız sayılıyor: "kontrol edilemedi" ile "temiz" aynı şey değil.
Depo kökünden çalıştırılır: python3 scripts/nuget-acik-kontrolu.py
"""
import glob
import json
import subprocess
import sys

sys.stdout.reconfigure(encoding="utf-8")

projeler = sorted(glob.glob("backend/src/*/*.csproj") + glob.glob("backend/tests/*/*.csproj"))
if not projeler:
    sys.exit("Proje bulunamadı — betik depo kökünden mi çalıştırıldı?")

acik_paketler: set[str] = set()
sorunlar: list[str] = []


def sorunlari_topla(kaynak: dict, proje: str) -> None:
    for sorun in kaynak.get("problems", []):
        if sorun.get("level", "").lower() == "error":
            sorunlar.append(f"{proje}: {sorun.get('text')}")


for proje in projeler:
    subprocess.run(["dotnet", "restore", proje], check=True, stdout=subprocess.DEVNULL)
    sonuc = subprocess.run(
        ["dotnet", "list", proje, "package", "--vulnerable", "--include-transitive", "--format", "json"],
        capture_output=True, encoding="utf-8", errors="replace",
    )
    if sonuc.returncode != 0:
        sorunlar.append(f"{proje}: dotnet list {sonuc.returncode} döndü\n{sonuc.stdout}{sonuc.stderr}")
        continue
    veri = json.loads(sonuc.stdout)
    sorunlari_topla(veri, proje)
    for p in veri.get("projects", []):
        sorunlari_topla(p, proje)
        for cerceve in p.get("frameworks", []):
            for paket in cerceve.get("topLevelPackages", []) + cerceve.get("transitivePackages", []):
                for acik in paket.get("vulnerabilities", []):
                    # Aynı paket birden çok projede geçiyor; bir kez yazılsın.
                    acik_paketler.add(
                        f"{paket['id']} {paket.get('resolvedVersion')}: "
                        f"{acik.get('severity')} {acik.get('advisoryurl')}")
    print(f"tarandı: {proje}")

for satir in sorted(acik_paketler):
    print(f"AÇIK: {satir}")
for satir in sorunlar:
    print(f"KONTROL EDİLEMEDİ: {satir}")
if acik_paketler or sorunlar:
    sys.exit(1)
print(f"{len(projeler)} projede bilinen açık yok.")
