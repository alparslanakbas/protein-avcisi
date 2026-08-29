# Tasarım QA Raporu — Rehber İçerik Merkezi

## Görsel kaynak ve doğrulama yüzeyleri

- Seçilen mockup: `C:\Users\legol\.codex\generated_images\01a039df-6da0-72a3-8e8b-0393e0e7f03b\exec-25183469-ee5e-4a42-bfe3-905bffd7ec0c.png`
- Eş ölçülü kaynak/uygulama karşılaştırması: `design-qa-rehber/source-vs-implementation.png`
- Üst bölüm odaklı karşılaştırma: `design-qa-rehber/focused-top-source-vs-implementation.png`
- Alt bölüm odaklı karşılaştırma: `design-qa-rehber/focused-bottom-source-vs-implementation.png`
- Masaüstü açık tema: `design-qa-rehber/rehber-desktop-light-1487x1058.png`
- Masaüstü koyu tema: `design-qa-rehber/rehber-desktop-dark-1487x1058.png`
- Mobil açık tema: `design-qa-rehber/rehber-mobile-light.png`
- Mobil koyu tema: `design-qa-rehber/rehber-mobile-dark.png`
- Masaüstü doğrulama yüzeyi: 1487 × 1058 CSS pikseli; seçilen mockupla aynı piksel ölçüsü.
- Mobil doğrulama yüzeyi: 390 × 844 CSS pikseli.

## Uygulanan tasarım ve içerik

- Rehber indeksi, seçilen “Learning Paths” yönünde arama ve hedef bazlı başlangıç rotaları olan bir içerik merkezine dönüştürüldü.
- Üç rota, gerçek API makalelerini slug üzerinden eşliyor; bulunmayan makale veya uydurma içerik gösterilmiyor.
- Son eklenen üç rehber gerçek `publishedAt` alanıyla ve `tr-TR` tarih formatıyla listeleniyor; kalan gerçek içerikler iki sütunlu konu listesine dağıtılıyor.
- Hesaplama ve kategori kısayolları mevcut rotalara bağlandı. Tüm ikonlar projenin Phosphor ailesinden, konuya göre semantik ve renk kodlu.
- Açık/koyu tema Hybrid Nocturne tokenlarıyla çalışıyor; tasarım basit renk terslemesine dayanmıyor.
- Masaüstü yoğunluğu, seçilen referanstaki bütün ana içeriği tek 1487 × 1058 görünümüne sığdıracak şekilde eşlendi.

## Etkileşim ve responsive kontrolü

- Rehber araması gerçek zamanlı çalışıyor. “kreatin” sorgusu üç gerçek sonucu doğru başlıklarla döndürdü; temizleme eylemi normal görünüme geri dönüyor.
- Masaüstü ve mobilde açık/koyu tema düğmeleri çalışıyor.
- Mobilde rota kartları tek sütuna, yardımcı kısayollar iki sütuna, konu listeleri tek sütuna geçiyor.
- 390 px mobil görünümde `scrollWidth` ile `clientWidth` eşit; yatay taşma yok.
- Uzun başlıklar kart dışına taşmıyor; metin hiyerarşisi ve bağlantı hedefleri korunuyor.
- Tarayıcı konsolunda hata veya uyarı yok.

## Karşılaştırma ve düzeltme geçmişi

1. İlk uygulamada kartların `data-tone` renkleri daha yüksek özgüllüklü varsayılan mor değişken tarafından eziliyordu. Rehber kapsamına özel ton seçicileriyle mor, mavi, yeşil, kırmızı ve turuncu ayrımı düzeltildi.
2. İlk masaüstü karşılaştırmasında alt konu ve bilgilendirme bölümü referans görünümünün altına taşıyordu. Kart, satır ve bölüm aralıkları referans ölçülerine göre sıkılaştırıldı.
3. Rota ikonları ve konu ikonları referansa göre fazla soluktu. Renkli dolu ikon yuvalarına geçirilerek hiyerarşi eşlendi.
4. Ana başlık ve arama alanı referanstan genişti. Başlık üst sınırı 42 px, arama genişliği 500 px yapıldı.
5. Son eş ölçülü karşılaştırmada bütün ana bölümler ve bilgilendirme bandı 1058 px yüksekliğe sığıyor; P0, P1 veya P2 düzeyinde açık fark kalmadı.

## Teknik doğrulama

- `npm run build`: başarılı.
- Angular tarayıcı ve SSR paketleri üretildi; rehber lazy chunk'ı yaklaşık 10 kB.
- Üretim SSR sunucusu başarıyla başladı. Yerel HTTP isteği, projede önceden bulunan boş `allowedHosts` kuralı nedeniyle reddediliyor; bu değişiklik kapsamındaki bir render veya derleme hatası değil.
- Geliştirme önizlemesi production API verisiyle doğrulandı.

## Sonuç

P0, P1 veya P2 seviyesinde açık görsel/işlevsel sorun kalmadı.

final result: passed

---

# Önceki Tasarım QA Kayıtları

# Tasarım QA Raporu — Kategoriler ve Hesaplama

## Görsel kaynak ve doğrulama yüzeyleri

- Seçilen mockup: `C:\Users\legol\AppData\Local\Temp\codex-clipboard-95bdad81-64ee-41b4-9251-adc9895d493f.png`
- Birleşik kaynak/uygulama karşılaştırması: `design-qa-directory/source-vs-implementation.png`
- Odaklı karşılaştırma: `design-qa-directory/focused-source-vs-implementation.png`
- Kategori masaüstü açık tema: `design-qa-directory/category-desktop-light.png`
- Hesaplama masaüstü koyu tema: `design-qa-directory/calculator-desktop-dark.png`
- Kategori mobil koyu tema: `design-qa-directory/category-mobile-dark.png`
- Kategori mobil açık tema (son): `design-qa-directory/category-mobile-light-final.png`
- Hesaplama mobil açık tema: `design-qa-directory/calculator-mobile-light.png`
- Hesaplama mobil koyu tema: `design-qa-directory/calculator-mobile-dark.png`
- Masaüstü tarayıcı yüzeyi: 1440 × 1024 CSS pikseli (yakalanan uygulama yüzeyi 1265 × 712 piksel)
- Mobil tarayıcı yüzeyi: 390 × 844 CSS pikseli, yoğunluk 1×

## Görsel karşılaştırma

- Tipografi: mevcut Inter ailesi korundu; başlık, açıklama, kart başlığı ve yardımcı metin hiyerarşisi mockupa yaklaştırıldı.
- Yerleşim: kategoriler masaüstünde üç, mobilde iki sütun; hesaplama ana araçları masaüstünde dört, mobilde iki sütun. Doz araçları daha kompakt satırlar olarak ayrıldı.
- Renkler: Hybrid Nocturne tokenları kullanıldı. Açık ve koyu temada renkli ikon yuvaları, sınırlar ve yüzey kontrastları ayrı ayrı doğrulandı.
- İkonlar: mevcut Phosphor ikon ailesindeki semantik simgeler kullanıldı; sahte veya elle çizilmiş varlık eklenmedi.
- İçerik: kategori adetleri üretim API verisinden, açıklamalar ve hesaplama tanımları mevcut gerçek kaynaklardan geliyor; uydurma veri eklenmedi.

## Etkileşim kontrolü

- Kategori araması gerçek zamanlı filtreliyor.
- `Tümü`, `Performans`, `Beslenme` ve `Kilo Kontrolü` filtreleri çalışıyor.
- `Performans` seçimi yalnızca Amino Asitler, Kreatin ve Pre-Workout kartlarını gösteriyor.
- Hesaplama sayfasındaki `Beslenme ve Vücut` / `Takviye Dozu` kontrolü ilgili bölüme kaydırıyor ve seçili durumunu erişilebilir biçimde güncelliyor.
- Kart bağlantıları ve klavye odak stilleri korunuyor.
- Tarayıcı konsolunda hata yok.

## Düzeltme geçmişi

1. İlk mobil kontrolde dokuzuncu kategori kartı iki sütunlu ızgarada yarım genişlikte kalıyordu.
2. Son tek kart, tek sayıda öğe olduğunda iki sütunu kapsayacak şekilde düzeltildi.
3. Düzeltme sonrası kart genişliği ve ızgara genişliği aynı ölçüldü: 366 piksel.
4. Uzun kategori ve hesaplama başlıklarında taşma olmadığı açık/koyu temada doğrulandı.
5. Sabit mobil alt gezinme için mevcut sayfa alt boşluğu korundu; son kart ve eylemler gezinmenin arkasında kalmıyor.

## Teknik doğrulama

- `npm run build`: başarılı.
- Angular tarayıcı ve SSR paketleri üretildi.
- Üretim SSR sunucusu başlatıldı. Yerel HTTP isteği, projede önceden bulunan boş `allowedHosts` kuralı nedeniyle reddediliyor; bu değişiklik kapsamındaki bir render veya derleme hatası değil.
- Geliştirme önizlemesinde gerçek production API ile her iki sayfa ve iki tema doğrulandı.

## Sonuç

P0, P1 veya P2 seviyesinde açık görsel/işlevsel sorun kalmadı.

final result: passed

---

# Önceki Tasarım QA Kayıtları

## Referans ve sonuç

- Görsel referans: `C:\Users\legol\AppData\Local\Temp\codex-clipboard-8bd718e7-4ea1-42e2-b618-d7c30ee264f3.png`
- Son masaüstü ekranı: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-home-light-final.png`
- Son mobil ekranı: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-home-mobile-final.png`
- Kategori mobil ekranı: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-category-mobile-final.png`
- Ürün modalı ekranı: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-product-modal-final.png`
- Masaüstü görünüm alanı: 1488 × 1058 CSS pikseli
- Mobil görünüm alanı: 390 × 844 CSS pikseli
- Doğrulanan durum: açık tema, ana sayfa, mağaza kampanyaları sekmesi, gerçek API verisi

## Karşılaştırma geçmişi

1. İlk mobil kontrolde kahraman alanı gereğinden uzun ve fiyat bölümü sıkışıktı. Mobil kart tek kolonlu düzene çevrildi, grafik küçük ekranda kaldırıldı ve ana eylem tam genişlik yapıldı.
2. Açık temadaki ikincil metinlerin kontrastı zayıftı. Açık tema nötr renkleri daha koyu ve okunaklı hale getirildi.
3. Masaüstünde kahraman alanı ile kategori şeridi referansa göre fazla yoğundu. Sol metin sütunu, kart oranı ve kategori boşlukları yeniden ayarlandı.
4. Son kontrolde ana başlık iki satıra düşüyordu. Masaüstü tipografisi tek satır korunacak şekilde düzeltildi.

## Son kontrol

- Yatay taşma: yok (mobil `scrollWidth` ile `clientWidth` eşit).
- Tarayıcı konsolu: hata ve uyarı yok.
- Açık/koyu tema kontrolü: çalışıyor.
- Ürün modalı açma/kapatma: çalışıyor.
- Kategori sayfası mobil düzeni: ürün kartı düzenine dönüşüyor.
- Referanstaki doğrulanmamış puan, kargo ve benzeri bilgiler eklenmedi; yalnızca API'nin sağladığı gerçek veriler gösteriliyor.
- Bilinen uyarı: başlangıç paketi mevcut 300 kB bütçesini yaklaşık 59 kB aşıyor; üretim derlemesini engellemiyor.

## Semantik ikon geçişi — 2026-08-26

### Referanslar ve son uygulama ekranları

- Kategori şeridi referansı: `C:\Users\legol\AppData\Local\Temp\codex-clipboard-794161f8-866b-4350-97ab-9eb75be3df2a.png`
- Kategori şeridi sonucu: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-icons-category-strip-final2.png`
- Kategori menüsü referansı: `C:\Users\legol\AppData\Local\Temp\codex-clipboard-3764e050-0ac7-4428-9a8d-df7602066ca3.png`
- Kategori menüsü sonucu: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-icons-category-menu-final3.png`
- Hesaplama menüsü referansı: `C:\Users\legol\AppData\Local\Temp\codex-clipboard-f4f5fc17-d36d-44b6-8197-a358a126d88d.png`
- Hesaplama menüsü sonucu: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-icons-calculator-menu-final3.png`
- Görünüm alanı: 1290 × 800 CSS pikseli, yoğunluk 1
- Durumlar: koyu tema; ana sayfa kategori şeridi; kategori menüsü açık; `/kategori/kreatin` sayfasında hesaplama menüsü açık

### Tespitler ve düzeltmeler

1. İlk kontrolde Kilo & Hacim ile L-Carnitine & CLA kategorileri üç nokta yedek ikonuna düşüyordu. Tüm kategori slug'ları merkezi ve semantik Phosphor ikon eşlemesine alındı.
2. Kategori açılır menüsünde her satır aynı küp ikonunu kullanıyordu. Ana sayfa kategori şeridi ile üst menü aynı ortak eşlemeyi kullanacak şekilde birleştirildi.
3. Hesaplama menüsünde dokuz araç aynı hesap makinesi ikonunu kullanıyordu. Protein, kalori, VKİ, su ve takviye dozları için ayrı ikonlar tanımlandı.
4. İlk uygulamada hesaplama ikonlarının etrafına eklenen daireler referansa göre menüyü gereksiz genişletiyordu. Daireler kaldırılarak satır yoğunluğu ve hiyerarşi korundu.
5. Son karşılaştırmada P0, P1 veya P2 düzeyinde görsel ya da işlevsel fark kalmadı. P3 notu: ikon şekilleri, kullanıcının ilgili ikon talebi doğrultusunda referanstaki genel simgelerden bilinçli olarak daha semantiktir.

### Değişmeden korunan yüzeyler

- Tipografi, yerleşim, boşluk sistemi ve tema tokenları değiştirilmedi.
- Metinler ve bağlantı hedefleri değiştirilmedi.
- Yeni raster varlık eklenmedi; mevcut Phosphor ikon ailesi kullanıldı.
- Kategori ve hesaplama menülerinin açma/kapatma etkileşimleri çalışıyor.
- Tarayıcı konsolunda hata veya uyarı yok.
- Angular üretim ve SSR derlemesi başarılı; yalnızca mevcut paket bütçesi uyarısı sürüyor.

## Hero mağaza butonu taşma düzeltmesi — 2026-08-26

### Görsel kanıt

- Kaynak görsel: `C:\Users\legol\AppData\Local\Temp\codex-clipboard-c3d06545-358a-40a6-b1b7-75b05bd41dc1.png`
- Masaüstü uygulama ekranı: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-hero-store-button-final.png`
- Odaklanmış eş boyutlu karşılaştırma: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-hero-store-button-focused-final.png`
- Mobil kontrol ekranı: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-hero-store-button-mobile-final.png`
- Kaynak ve odaklanmış sonuç: 1489 × 268 piksel; yoğunluk 1
- Masaüstü CSS görünümü: 1489 piksel içerik genişliği; koyu tema; ana sayfa hero alanı
- Mobil CSS görünümü: 375 piksel içerik genişliği; koyu tema; ana sayfa hero alanı

### Karşılaştırma geçmişi

1. İlk görüntüde P2: hero kartının son grid kolonu 150 px idi. Sol ayraç ve iç boşluk sonrasında ana eyleme yaklaşık 129 px kaldığı için “Mağazaya git” iki satıra kırılıyordu.
2. Son grid kolonu 180 px'e çıkarıldı. Her iki aksiyon tam genişlik ve `white-space: nowrap` aldı; dış bağlantı ikonu küçülmeye karşı korundu.
3. Son görselde ana eylem ve ikincil eylem tek satır, eş genişlikte ve ortalanmış durumda. Masaüstünde buton `clientWidth = scrollWidth = 163`; sayfada yatay taşma yok.
4. Mobil kontrolde hero eylemi `clientWidth = scrollWidth = 323`; sayfada `clientWidth = scrollWidth = 375`. Düzeltme mobil yerleşimi bozmadı.

### Gerekli doğruluk yüzeyleri

- Tipografi: yazı ailesi, ağırlık, boyut ve satır yüksekliği korunuyor; yalnızca istenmeyen satır kırılması kaldırıldı.
- Boşluk ve yerleşim: yalnızca hero aksiyon kolonu 30 px genişletildi; kart yüksekliği, dış boşluklar ve diğer kolonların hiyerarşisi korundu.
- Renk ve tokenlar: değişiklik yok.
- Görsel kalite ve varlıklar: ürün görseli ile mevcut Phosphor dış bağlantı ikonu korundu; yeni raster/SVG varlık eklenmedi.
- Metin ve içerik: “Mağazaya git” ve “Fiyat geçmişini aç” metinleri ile bağlantı hedefleri değişmedi.
- Etkileşim: bağlantı ve modal butonu aynı davranışı sürdürüyor; yalnızca düzen sınıfları değişti.
- Tarayıcı konsolunda hata veya uyarı yok.
- Angular üretim ve SSR derlemesi başarılı; mevcut başlangıç paketi bütçe uyarısı devam ediyor.
- Son kontrolde P0, P1 veya P2 bulgusu kalmadı.

## Açık tema açılır menü kontrastı — 2026-08-26

### Görsel kanıt

- Kategori sorun referansı: `C:\Users\legol\AppData\Local\Temp\codex-clipboard-7677f59b-7524-44e1-a893-cfacb0f8f2e5.png` (1141 × 318 px)
- Kategori düzeltilmiş odak görüntüsü: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-light-home-category-menu-focused-final.png` (1141 × 318 px)
- Hesaplama sorun referansı: `C:\Users\legol\AppData\Local\Temp\codex-clipboard-a1884e5d-0696-4ec3-b0c5-de82ff4a9be1.png` (222 × 452 px)
- Hesaplama düzeltilmiş odak görüntüsü: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-light-calculator-menu-focused-final.png` (222 × 452 px)
- Ana sayfa tam görüntüsü: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-light-home-category-menu-final.png`
- İç sayfa kategori tam görüntüsü: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-light-shared-category-menu-final.png`
- Hesaplama tam görüntüsü: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-light-calculator-menu-final.png`
- Durum: açık tema; ana sayfa kategori menüsü açık; `/kategori/kreatin` ortak kategori ve hesaplama menüleri açık; yoğunluk 1

### Karşılaştırma geçmişi

1. İlk görüntülerde P1: koyu marka üst barı `--noc-text` değişkenini beyaza sabitliyor, beyaz açılır menüler de bu değeri miras alıyordu. Kategori ve hesaplama bağlantıları beyaza yakın renkte kaldığı için temel gezinme okunamıyordu.
2. Ana sayfadaki kategori menüsü ile ortak header'daki kategori ve hesaplama menülerine tema-duyarlı yüzey metni eklendi: açık temada `--noc-neutral-900`, koyu temada mevcut `--noc-text`.
3. Son ölçümde açık tema metni `rgb(32, 36, 56)`, yüzey `rgb(255, 255, 255)` ve kontrast oranı 15.32:1. Üç menü de okunaklı.
4. Koyu tema kontrolünde metin `rgb(247, 247, 252)`, yüzey `rgb(29, 34, 56)` kaldı; mevcut koyu görünüm bozulmadı.

### Gerekli doğruluk yüzeyleri

- Tipografi: aile, ağırlık, boyut, satır yüksekliği ve metin kırılmaları değişmedi; yalnızca okunabilir renk düzeltildi.
- Boşluk ve yerleşim: menü genişliği, grid, padding, köşe yarıçapı ve gölge değişmedi.
- Renk ve tokenlar: açık/koyu tema için mevcut Nocturne tokenları kullanıldı; sabit yeni renk eklenmedi.
- Görsel kalite ve varlıklar: semantik Phosphor ikonları ve vurgu renkleri korundu; yeni varlık eklenmedi.
- Metin ve içerik: kategori/hesaplama adları ile bağlantı hedefleri değişmedi.
- Etkileşim: kategori ve hesaplama menülerinin açma/kapatma durumları doğrulandı.
- Tarayıcı konsolunda hata veya uyarı yok.
- Angular üretim ve SSR derlemesi başarılı; mevcut başlangıç paketi bütçe uyarısı devam ediyor.
- Son kontrolde P0, P1 veya P2 bulgusu kalmadı.

## Mobil ürün görünüm sekmeleri — 2026-08-26

### Görsel kanıt

- Düzeltilmiş mobil görüntü: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-mobile-all-products-tab-final.png`
- Görünüm: 390 × 844 px; gerçek sayfa içerik genişliği 375 px; koyu tema
- Seçili durum: `Tümü`; açıklama: “Takipteki tüm ürünler.”

### Karşılaştırma geçmişi

1. P1 kök neden: `Tümü` düğmesinde `hidden sm:block` sınıfları vardı; 640 px altındaki ekranlarda üçüncü görünüm seçeneği DOM'da olsa da kullanıcıya gösterilmiyordu.
2. Mobil gizleme kaldırıldı. Üç düğmenin mobil yatay boşluğu ve yazı boyutu küçültildi; `sm` ve üzerindeki mevcut masaüstü ölçüleri korundu.
3. Son ölçümde sekme grubunda `clientWidth = scrollWidth = 302`, sayfada `clientWidth = scrollWidth = 375`. Üç düğme de aynı satırda ve yatay taşma yok.
4. `Gerçek düşüşler`, `Mağaza kampanyaları` ve `Tümü` tıklanarak her modun kendine ait açıklama metnini gösterdiği doğrulandı.
5. Yerel tarayıcıdaki istemci veri isteği, canlı API'nin `localhost:4000` kaynağına CORS izni vermemesi nedeniyle beklenen hata durumuna geçti. Bu, sekme değişikliğinden bağımsızdır; üretim SSR derlemesi canlı API verisini başarıyla aldı.

### Gerekli doğruluk yüzeyleri

- Tipografi: mobilde 12 px, `sm` ve üzerinde mevcut 14 px boyut korunuyor.
- Boşluk ve yerleşim: üç sekme tek satırda; çevre bölüm ve açıklama konumu değişmedi.
- Renk ve tokenlar: seçili/pasif renkler ile açık/koyu tema davranışı değişmedi.
- Metin ve içerik: üç görünüm adı ve açıklamaları aynen korundu.
- Etkileşim: üç görünüm seçeneğinin tamamı mobilde erişilebilir ve seçilebilir.
- Angular üretim ve SSR derlemesi başarılı; mevcut başlangıç paketi bütçe uyarısı devam ediyor.
- Son kontrolde bu istek kapsamında P0, P1 veya P2 bulgusu kalmadı.

## Mobil iki sütunlu ürün listesi — 2026-08-26

### Görsel kanıt

- Gerçek ürün verili mobil görüntü: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-mobile-two-column-products-final.png`
- Görünüm: 390 × 844 px; gerçek sayfa içerik genişliği 375 px; açık tema

### Karşılaştırma geçmişi

1. Önceki mobil düzende her ürün bütün satırı kaplıyordu; kullanıcı aynı anda yalnızca tek ürün görebildiği için listeyi gereğinden fazla kaydırmak zorunda kalıyordu.
2. 640 px ve altında ürün listesi iki eşit sütunlu bir grid'e çevrildi. Gerçek kontrolde liste genişliği 351 px, her kart genişliği yaklaşık 171 px.
3. Dar kartlarda tekrarlanan “Fiyat geçmişi takipte”, son kontrol zamanı ve resmî site açıklaması gizlendi. Ürün görseli/adı, marka-boyut, fiyat, indirim, servis fiyatı, mağaza ve karşılaştırma aksiyonları korundu.
4. Aynı satırdaki kartlar eşit yüksekliğe uzuyor; mağaza ve karşılaştırma butonları kartın altına hizalanıyor.
5. Son ölçümde liste ve kartlarda `clientWidth = scrollWidth`; sayfada `clientWidth = scrollWidth = 375`. Yatay taşma yok.

### Gerekli doğruluk yüzeyleri

- Tipografi: ürün başlığı mobilde 13 px ve iki satırla sınırlı; fiyat 18 px ile görsel önceliğini koruyor.
- Boşluk ve yerleşim: 10 px sütun aralığı ve kart iç boşluğu kullanılıyor; dokunma aksiyonları tam kart genişliğinde.
- Renk ve tokenlar: mevcut Nocturne açık/koyu tema tokenları aynen kullanılıyor.
- Metin ve içerik: fiyat, indirim ve gerçek ürün verileri değiştirilmedi veya tahmin edilmedi.
- Etkileşim: ürün detayı, mağaza ve karşılaştırma aksiyonları görünür kaldı.
- Angular üretim ve SSR derlemesi başarılı; mevcut başlangıç paketi bütçe uyarısı devam ediyor.
- Son kontrolde bu istek kapsamında P0, P1 veya P2 bulgusu kalmadı.

## Kategori menüsü dikey sıkışma ve kırpılma — 2026-08-27

### Görsel kanıt

- Sorun referansı: `C:\Users\legol\AppData\Local\Temp\codex-clipboard-58b1fcc5-2072-43e2-9b45-23ae4776ff00.png`
- Düzeltilmiş koyu tema: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-category-menu-spacing-dark-final.png`
- Düzeltilmiş açık tema: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-category-menu-spacing-light-final.png`
- Kontrol görünümü: 1440 × 700 px; gerçek içerik genişliği 1425 px

### Karşılaştırma geçmişi

1. Dokuz kategori iki sütunda beş satıra yığılıyor, düşük dikey aralık nedeniyle menü aşağı doğru sıkışık görünüyordu.
2. Ana sayfadaki `.pa-hero-zone { overflow: hidden; }` kuralı menünün kategori şeridine uzanan alt bölümünü gerçekten kırpıyordu.
3. Ana sayfa ve ortak başlık aynı `pa-category-menu` bileşen sınıflarına geçirildi. Geniş ekranda üç sütun, daha dar masaüstünde iki sütun kullanılıyor.
4. Kategori satırları 52 px minimum yüksekliğe, 36 px ikon yüzeyine ve 6 px dikey aralığa çıkarıldı. Dokuz kategori geniş ekranda 3 × 3 diziliyor.
5. Hero taşması görünür yapıldı; dekoratif arka plan kapsayıcının sınırlarında kaldığı için görsel yan etki oluşmadan menünün tamamı kategori şeridinin üzerinde görünüyor.
6. Kısa ekranlar için menü yüksekliği ekranla sınırlandırıldı ve gerektiğinde yalnızca menünün kendi içinde dikey kaydırma etkinleştirildi.

### Gerekli doğruluk yüzeyleri

- Yerleşim: menü 720 px genişlikte, üç adet 226 px sütun; toplam yükseklik 246 px.
- Taşma: menüde `clientHeight = scrollHeight = 244`; sayfada `clientWidth = scrollWidth = 1425`.
- Açık tema: beyaz yüzey ve `rgb(32, 36, 56)` metin.
- Koyu tema: `rgb(29, 34, 56)` yüzey ve `rgb(247, 247, 252)` metin.
- Etkileşim: dış alana tıklayarak kapatma, kategori bağlantıları ve tüm kategoriler bağlantısı korundu.
- Angular üretim ve SSR derlemesi başarılı.
- Son kontrolde bu istek kapsamında P0, P1 veya P2 bulgusu kalmadı.

## E-posta şablonlarının Hybrid Nocturne geçişi — 2026-08-27

### Görsel kanıt

- Abonelik onay referansı: `C:\Users\legol\AppData\Local\Temp\codex-clipboard-0ac54508-012d-4b9c-967a-f15b4a5b70a6.png`
- Abonelik onay sonucu: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-email-confirmation-desktop-final.png`
- Onay referans/sonuç karşılaştırması: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-email-confirmation-comparison-final.png`
- Haftalık özet referansı: `C:\Users\legol\AppData\Local\Temp\codex-clipboard-ecb4a34a-0495-44a7-8107-4ac161688f79.png`
- Haftalık özet sonucu: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-email-digest-desktop-final2.png`
- Özet referans/sonuç karşılaştırması: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-email-digest-comparison-final.png`
- Mobil sonuçlar: `design-qa-email-confirmation-mobile-final.png`, `design-qa-email-digest-mobile-final.png`
- Kontrol görünümü: masaüstü 900 × 1200 px, mobil 390 × 900 CSS pikseli

### Karşılaştırma geçmişi

1. Onay e-postasının ilk uygulamasında başlık masaüstünde üç satıra kırılıyordu. Metin sütunu ve masaüstü başlık ölçüsü referanstaki iki satırlı hiyerarşiye yaklaştırıldı.
2. Haftalık özetin ilk uygulamasında fiyat etiketi görselinin koyu dikdörtgen arka planı görünüyordu. Varlık şeffaf PNG'ye çevrildi.
3. Haftalık özetin ana eylemi ilk uygulamada referansa göre dar kalıyordu. E-posta istemcileriyle uyumlu tablo tabanlı tam genişlik butonuna geçirildi.
4. Ürün görselleri, isimleri, fiyatları ve indirim oranları gerçek API verisiyle kontrol edildi; uydurma ürün bilgisi eklenmedi.
5. Abonelik onayı ve haftalık özet dışında fiyat alarmı ile favori hatırlatma e-postaları da aynı ortak marka kabuğuna geçirildi.

### Gerekli doğruluk yüzeyleri

- Masaüstü ve mobilde yatay taşma yok: `clientWidth = scrollWidth`.
- Altı bülten ürünü masaüstünde 2 × 3, mobilde altı adet tek sütunlu kart olarak render oluyor.
- Mobil ürün kartlarının her biri 364 px; bülten ana eylemi 324 px tam içerik genişliğinde.
- Onay ve özet önizlemelerindeki bütün görseller `complete = true` ve geçerli doğal genişliğe sahip.
- Onay, ürün, ana sayfa ve abonelikten çık bağlantıları gerçek hedeflere bağlı.
- Backend derlemesi 0 uyarı ve 0 hata ile başarılı.
- Angular production/SSR derlemesi başarılı; SSR sunucusunda `weekly-price-tag.png` 200 durum kodu ve doğru `image/png` türüyle sunuldu.
- Son karşılaştırmada bu istek kapsamında P0, P1 veya P2 bulgusu kalmadı. P3: referanstaki iki adım arasındaki dekoratif noktalı ok, e-posta istemcisi tutarlılığı için bilinçli olarak kullanılmadı.

## Abonelik onayı başarı ekranı — 2026-08-28

### Görsel kanıt

- Kaynak görsel: `C:\Users\legol\.codex\generated_images\01a039df-6da0-72a3-8e8b-0393e0e7f03b\exec-95237034-7a7f-45d0-bdbd-6694d93519aa.png`
- Masaüstü uygulama görüntüsü: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-subscription-confirmed-desktop-stable.png`
- Mobil uygulama görüntüsü: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-subscription-confirmed-mobile.png`
- Aynı girdide yan yana karşılaştırma: `C:\Users\legol\OneDrive\Masaüstü\indirim-takip-sistemi\design-qa-subscription-confirmed-comparison.png`
- Kaynak piksel boyutu: 1536 × 1024. Masaüstü uygulama çıktısı: 1265 × 1027 piksel; CSS görünümü 1280 × 720, `deviceScaleFactor = 1`. Mobil çıktı: 375 × 901 piksel; CSS görünümü 390 × 844, `deviceScaleFactor = 1`.
- Karşılaştırma normalizasyonu: kaynak ve uygulamadaki merkez içerik alanları 1122 × 936 piksel kırpılıp 1120 × 934 piksele eşitlendi.
- Durum: başarılı abonelik onayı; açık sayfa zemini, Hybrid Nocturne başarı kartı ve güven şeridi.

### Bulgular ve karşılaştırma geçmişi

1. İlk uygulamada kart 1040 × 630 px ile referansın yaklaşık 1120 × 820 px oranından belirgin biçimde küçüktü; başlık, açıklama, eylem ve güven şeridi de aynı nedenle düşük yoğunlukta kalıyordu. Bu P2 bulgusu kartı 1120 px'e, koyu alanı 675 px'e ve güven şeridini 145 px'e çıkararak giderildi.
2. İlk uygulamadaki “ABONELİK AKTİF” etiketi referanstaki zarf simgesini taşımıyordu. Mevcut gerçek `step-confirm.png` varlığı kullanıldı; HTML/CSS çizimi veya geçici simge eklenmedi.
3. Metin ölçeği, düğme yüksekliği ve güven şeridi referansa göre büyütüldü. `e-postana` ifadesinin tireden yanlış satır kırması, kırılmaz tire kullanılarak düzeltildi.
4. İlk mobil ekran görüntüsü tarayıcının görünüm değişiminden hemen sonra alındığı için geçersiz biçimde kırpılmıştı. Kararlı yeniden yükleme sonrası 390 × 844 görünüm tekrar yakalandı; kart, eylem ve varlıkların tamamı görünür, yatay taşma yok.
5. Son yan yana karşılaştırmada ana kompozisyon, kart oranı, başlık hiyerarşisi, fiyat çizgisi/check görseli, eylem ve güven şeridi arasında P0, P1 veya P2 düzeyinde fark kalmadı.

### Gerekli doğruluk yüzeyleri

- Tipografi: Arial/Helvetica sistem yedeğiyle referanstaki ağır başlık ve okunaklı destek metni hiyerarşisi korundu; masaüstü başlık 72 px, mobil başlık 40 px. Metin kesilmesi veya taşması yok.
- Boşluk ve yerleşim: masaüstü kart 1120 px genişlik ve yaklaşık 829 px toplam yükseklikte; mobil kart 347 px genişlikte tek sütun. Kart yarıçapı, iç boşluklar, CTA hizası ve güven şeridi ritmi referansla eşleşiyor.
- Renk ve tokenlar: sıcak beyaz sayfa zemini, koyu lacivert kart, mor eylem ve mint güven vurgusu Hybrid Nocturne diliyle uyumlu. Kontrast okunabilir.
- Görsel kalite: fiyat sinyali özel raster varlık olarak 1200 × 800 kaynakla sunuluyor; favicon, zarf ve güven kalkanı gerçek proje varlıkları. Üç görsel de tarayıcıda başarıyla yüklendi.
- Metin ve içerik: “Aboneliğin onaylandı!”, destek metni, “İndirimleri Gör” ve güven mesajı referans içeriğini koruyor. Uydurma veri yok.
- Etkileşim: ana marka bağlantısı ve “İndirimleri Gör” CTA'sı gerçek frontend köküne bağlı; CTA masaüstünde 416 × 76 px, mobilde 293 × 60 px. Hover ve klavye odak stilleri tanımlı.
- Duyarlılık: mobilde `scrollWidth = clientWidth = 375`; yatay taşma yok. Sayfanın 901 px toplam yüksekliği doğal dikey kaydırmayla erişilebilir.
- Tarayıcı konsolunda hata veya uyarı yok. Backend derlemesi 0 uyarı ve 0 hata ile başarılı; test komutu hatasız tamamlandı. Yerel makinede veritabanı bağlantı sırrı bulunmadığı için gerçek token endpoint'i DB üzerinden tetiklenemedi; aynı derlenmiş backend metodunun ürettiği HTML doğrudan render edilerek doğrulandı.
- Odaklanmış ayrı kırpma gerekmedi: tek sayfalık yüzeyin yan yana normalize edilmiş tam görünümünde etiket, tipografi, CTA, görsel ve güven şeridi okunabilir ölçüdeydi.
- P3 kabulü: kaynak mockuptaki koyu logo kutusu yerine ürünün gerçek mor favicon'u kullanıldı; bu, mevcut marka kimliğini koruyan bilinçli bir ürün kararıdır.

final result: passed
