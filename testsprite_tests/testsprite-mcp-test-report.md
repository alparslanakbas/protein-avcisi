
# TestSprite AI Testing Report(MCP)

---

## 1️⃣ Document Metadata
- **Project Name:** asd (Protein Avcısı — backend)
- **Date:** 2026-08-18
- **Prepared by:** TestSprite AI Team + Claude (doğrulama geçişi)
- **Kapsam:** Sadece TC001–TC009 (TC010 bilinçli olarak atlandı — `/go/{id}` production `ClickCount` sayacını kalıcı olarak artırıyor)

---

## 2️⃣ Requirement Validation Summary

### Requirement: Ürün/indirim listeleme uç noktaları (`/api/deals`, `/api/products`, `/api/store-deals`)

#### Test TC001 — GET /api/deals filtreleme ve sayfalama
- **Test Code:** [TC001_get_api_deals_with_filtering_and_pagination.py](./TC001_get_api_deals_with_filtering_and_pagination.py)
- **Test Error:** `AssertionError: 'totalItems' key missing in response`
- **Status:** ❌ Failed (test scripti hatalı varsayım — **gerçek bug değil**)
- **Analysis / Findings:** Yanlış pozitif. Test scripti sayfalama alanının adının `totalItems` (ve/veya `pagination` adında iç içe bir obje) olduğunu varsaymış. Gerçek API sözleşmesi (CLAUDE.md'de belgeli, `PagedResult`) düz/top-level `totalCount`/`page`/`pageSize`/`totalPages` kullanıyor — `curl "http://localhost:5156/api/deals?page=1&pageSize=2"` ile doğrulandı, yanıt `{"items":[...],"totalCount":14,"page":1,"pageSize":2,"totalPages":7}` şeklinde geliyor. Endpoint'in kendisi doğru çalışıyor, testin beklediği şema yanlış.

#### Test TC002 — GET /api/products filtreleme ve sayfalama
- **Test Code:** [TC002_get_api_products_with_filtering_and_pagination.py](./TC002_get_api_products_with_filtering_and_pagination.py)
- **Test Error:** `AssertionError: Product missing required identifier field 'id' or 'key'`
- **Status:** ❌ Failed (test scripti hatalı varsayım — **gerçek bug değil**)
- **Analysis / Findings:** Yanlış pozitif. Ürün nesnesindeki kimlik alanı `id` değil `productId` — DealDto'nun tasarımı bu şekilde (CLAUDE.md'de belgeli). `curl` ile doğrulandı: her üründe `"productId":603` gibi bir alan var, test scripti sadece `id`/`key` adlarını arıyordu.

#### Test TC003 — GET /api/store-deals filtreleme ve sayfalama
- **Test Code:** [TC003_get_api_store_deals_with_filtering_and_pagination.py](./TC003_get_api_store_deals_with_filtering_and_pagination.py)
- **Test Error:** `AssertionError: Response should contain 'pagination' key`
- **Status:** ❌ Failed (test scripti hatalı varsayım — **gerçek bug değil**)
- **Analysis / Findings:** TC001 ile aynı kök sebep — test sayfalama bilgisinin `pagination` adlı iç içe bir obje altında olacağını varsaymış, gerçek API düz/top-level alanlar kullanıyor. `curl "http://localhost:5156/api/store-deals?page=1&pageSize=1"` ile `totalCount`/`page`/`pageSize`/`totalPages`'ın top-level geldiği doğrulandı.

#### Test TC004 — GET /api/products/{id} geçerli ve geçersiz ID
- **Test Code:** [TC004_get_api_products_by_id_with_valid_and_invalid_ids.py](./TC004_get_api_products_by_id_with_valid_and_invalid_ids.py)
- **Status:** ✅ Passed
- **Analysis / Findings:** Geçerli bir ürün ID'si 200 + doğru veri döndürüyor. Geçersiz/var olmayan bir ID (bir GUID string'i, `{id:int}` route kısıtına uymadığı için) beklenen 404'ü doğru şekilde veriyor. Sorun yok.

### Requirement: Fiyat geçmişi ve sparkline uç noktaları

#### Test TC005 — GET /api/products/{id}/price-history geçerli/geçersiz `days`
- **Test Code:** [TC005_get_api_products_price_history_with_valid_and_invalid_days.py](./TC005_get_api_products_price_history_with_valid_and_invalid_days.py)
- **Test Error:** `AssertionError: Expected 400 for invalid days=0 but got 200`
- **Status:** ❌ Failed — **kısmen gerçek, kısmen kasıtlı davranış**
- **Analysis / Findings:** İki ayrı davranış karışmış durumda, ikisi de gerçek kodda doğrulandı:
  - `days=0`, `days=-1`, `days=999` gibi "mantıksız ama sayısal" değerler **kasıtlı olarak** 400 döndürmüyor — `Program.cs:288`'de `var windowDays = days is null or <= 0 ? 30 : days.Value;` ile sıfır/negatif değerler sessizce varsayılan 30 güne düşürülüyor. Bu tasarım tercihi (sıkı doğrulama yerine nazik/geriye dönük uyumlu davranış), gerçek bir hata değil ama REST açısından tartışmalı bir seçim — istenirse sıkılaştırılabilir.
  - **Gerçek bug:** `days=abc` gibi sayıya çevrilemeyen bir değer 400 DEĞİL **500** döndürüyor (`curl` ile doğrulandı: `{"message":"Beklenmeyen bir hata oluştu."}`). Kök sebep `Program.cs:125-137`'deki global `UseExceptionHandler` — ASP.NET Core'un query-string binding hatası için normalde ürettiği 400'ü (BadHttpRequestException), bu handler ayrım yapmadan yakalayıp genel 500'e çeviriyor. Aynı kök sebep TC007'deki gerçek bug ile birebir aynı (aşağıya bakın).

#### Test TC006 — GET /api/products/sparklines geçerli ID'ler ve `days`
- **Test Code:** [TC006_get_api_products_sparklines_with_valid_ids_and_days.py](./TC006_get_api_products_sparklines_with_valid_ids_and_days.py)
- **Status:** ✅ Passed
- **Analysis / Findings:** Toplu sparkline uç noktası beklendiği gibi çalışıyor, sorun yok.

### Requirement: Marka karşılaştırma

#### Test TC007 — GET /api/brand-comparison geçerli/geçersiz marka parametreleri
- **Test Code:** [TC007_get_api_brand_comparison_with_valid_and_invalid_brands.py](./TC007_get_api_brand_comparison_with_valid_and_invalid_brands.py)
- **Test Error:** `AssertionError: Expected 400 or 404 for invalid params {'brand1': 'hiq'}, got 500`
- **Status:** ❌ Failed — **GERÇEK BUG**
- **Analysis / Findings:** Doğrulandı. `brand2` (veya her iki parametre) hiç gönderilmeden `/api/brand-comparison`'a istek atılınca 500 dönüyor:
  - `curl "http://localhost:5156/api/brand-comparison?brand1=hiq"` → 500
  - `curl "http://localhost:5156/api/brand-comparison"` → 500
  - Kök sebep: `DealsQueryService.GetBrandComparisonAsync(string brand1, string brand2, ...)` — C#'ın nullable reference type (`string`, `?` yok) annotasyonu SADECE derleme zamanında uyarı verir, runtime'da zorlanmıyor; eksik query parametresi ASP.NET Core minimal API tarafından `null` olarak bind ediliyor (400 fırlatmıyor), sonra `DealsQueryService.cs:229-230`'daki `brand1.ToLower()`/`brand2.ToLower()` çağrısı `NullReferenceException` fırlatıyor, bu da global exception handler tarafından 500'e çevriliyor. Doğru davranış: eksik/boş marka parametresi 400 (Bad Request) dönmeli.
  - Boş string (`brand2=""`) verildiğinde ise 404 dönüyor (marka bulunamadı mantığına düşüyor, doğru) — sadece TAMAMEN EKSİK parametre null-ref'e yol açıyor.

### Requirement: Kupon ve rehber (makale) uç noktaları

#### Test TC008 — GET /api/coupons
- **Test Code:** [TC008_get_api_coupons_returns_active_coupon_codes.py](./TC008_get_api_coupons_returns_active_coupon_codes.py)
- **Status:** ✅ Passed
- **Analysis / Findings:** Aktif kupon listesi doğru şema ile dönüyor, sorun yok.

#### Test TC009 — GET /api/articles liste ve detay, geçerli/geçersiz slug
- **Test Code:** [TC009_get_api_articles_list_and_detail_with_valid_and_invalid_slugs.py](./TC009_get_api_articles_list_and_detail_with_valid_and_invalid_slugs.py)
- **Status:** ✅ Passed
- **Analysis / Findings:** Liste, geçerli slug detayı ve bilinmeyen/bozuk slug'larda 404 davranışı hepsi doğru, sorun yok.

---

## 3️⃣ Coverage & Matching Metrics

- **9/9** planlanan test (TC001–TC009) çalıştırıldı (TC010 kapsam dışı bırakıldı).
- Ham sonuç: **4/9 (%44.4) geçti**. Ancak 3 başarısızlık (TC001/TC002/TC003) test scriptinin yanlış şema varsayımından kaynaklanıyor — gerçek API doğru çalışıyor. Bunlar hariç tutulduğunda **gerçek isabet oranı 6/6 uç nokta doğru, 2 endpoint'te (price-history, brand-comparison) gerçek bir doğrulama/hata-yönetimi kusuru var.**

| Requirement                                   | Total Tests | ✅ Passed | ❌ Failed (gerçek bug) | ❌ Failed (yanlış pozitif) |
|------------------------------------------------|-------------|-----------|------------------------|------------------------------|
| Ürün/indirim listeleme (deals/products/store)   | 4           | 1         | 0                       | 3 (TC001, TC002, TC003)      |
| Fiyat geçmişi & sparkline                       | 2           | 1         | 1 (TC005 — kısmi)       | 0                             |
| Marka karşılaştırma                             | 1           | 0         | 1 (TC007)               | 0                             |
| Kupon & rehber                                  | 2           | 2         | 0                       | 0                             |
| **Toplam**                                      | **9**       | **4**     | **2**                   | **3**                        |

---

## 4️⃣ Key Gaps / Risks

1. **[Gerçek, düşük-orta risk] Global exception handler, istemci hatalarını (400) sunucu hatası (500) olarak maskeliyor.** `Program.cs:125-137`'deki `UseExceptionHandler`, ayrım yapmadan HER unhandled exception'ı genel 500'e çeviriyor. Bu, hem `days=abc` gibi geçersiz query tipi girişlerinde (ASP.NET Core normalde otomatik 400 üretirdi) hem de `brand-comparison`'daki eksik parametre null-ref'inde ortaya çıkıyor. Etkisi düşük (istemci tarafında zaten UI'lar geçerli değerler gönderiyor, dışarıdan kötü niyetli/hatalı bir istek en kötü ihtimalle yanlış HTTP status kodu görür) ama API sözleşmesi açısından yanıltıcı ve izlenebilirliği zorlaştırıyor (gerçek sunucu hatalarıyla istemci hataları aynı log/alarm seviyesinde görünüyor).
2. **[Gerçek, düşük risk] `/api/brand-comparison` eksik `brand1`/`brand2` parametresinde `NullReferenceException` fırlatıyor.** Doğru davranış 400 (Bad Request) olmalı — şu an istemci hatası bir sunucu hatası gibi loglanıyor/dönüyor.
3. **[Tasarım notu, aksiyon gerektirmiyor]** `/api/products/{id}/price-history`'de `days<=0` değerleri sessizce varsayılan 30 güne düşürülüyor — bu kasıtlı bir "nazik davranış" tercihi, sıkı REST doğrulaması değil. Mevcut haliyle sorun çıkarmıyor, sadece not edildi.
4. **[Test altyapısı notu]** TC001/TC002/TC003'teki 3 başarısızlık backend'de değil, TestSprite'ın otomatik ürettiği test scriptlerinin yanlış şema varsayımında (`totalItems`, `pagination` iç içe obje, `id` alan adı) — gerçek API sözleşmesiyle (`totalCount`, düz alanlar, `productId`) karşılaştırılıp doğrulandı. Regresyon riski taşımıyor.

**Önerilen sıradaki adım (kullanıcı onayı bekliyor, bu turda kod değişikliği yapılmadı):** `UseExceptionHandler`'a `BadHttpRequestException`/model-binding hatalarını 400'e, `brand-comparison`'a da açık bir null/boş kontrolü (400 Bad Request) eklemek — küçük, düşük riskli iki düzeltme.
