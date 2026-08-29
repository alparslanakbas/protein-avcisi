export interface ProductDescriptionInput {
  displayName: string;
  brandName: string;
  priceText: string;
  discountPercent: number;
  description: string | null | undefined;
}

// Arama sonucunda görünen açıklama. Önceden yalnızca fiyat cümlesinden
// ibaretti; artık 500'den fazla üründe markanın kendi açıklama metni
// veritabanında olduğu için ürünün NE OLDUĞUNU söyleyen bir cümle öne
// alınıyor, fiyat arkasına ekleniyor.
//
// Ham metin doğrudan kullanılamıyor: her markanın kendine özgü bir gürültüsü
// var (HIQ "Açıklama:" önekiyle, Hardline "... NEDİR ?:" başlığıyla, SSN ürün
// adını tekrarlayarak başlıyor; hepsinde sert boşluk karakterleri geçiyor).
// Aşağıdaki temizlik bu üç kalıba karşı gerçek üretim verisiyle denendi.
export function buildProductDescription(input: ProductDescriptionInput): string {
  const intro = extractIntro(input.description, input.displayName);

  if (!intro) {
    // Açıklaması olmayan ürünlerde eski şablon aynen kalıyor — boş bırakmıyoruz.
    return input.discountPercent > 0
      ? `${input.displayName} şu an ${input.priceText} — ${input.brandName} markasında %${input.discountPercent} doğrulanmış indirim. Fiyat geçmişini ProteinAvcısı'nda takip et.`
      : `${input.displayName} güncel fiyatı ${input.priceText}. ${input.brandName} markasının fiyat geçmişini ProteinAvcısı'nda takip et.`;
  }

  const priceSentence = input.discountPercent > 0
    ? `${input.priceText}, %${input.discountPercent} doğrulanmış indirim.`
    : `Güncel fiyatı ${input.priceText}.`;

  const full = `${intro} ${priceSentence} Fiyat geçmişi ProteinAvcısı'nda.`;
  // Google açıklamayı ~160 karakterde kesiyor; sığmıyorsa marka kuyruğunu
  // atıyoruz, çünkü ürünün ne olduğu ve fiyatı daha değerli.
  return full.length > 165 ? `${intro} ${priceSentence}` : full;
}

const LEADING_PUNCTUATION = /^[\s:：.,;·–—-]+/;

function extractIntro(raw: string | null | undefined, productName: string): string | null {
  if (!raw) return null;

  // Sert boşluk (U+00A0) üç markanın metninde de geçiyor.
  let text = raw.replace(/ /g, ' ').replace(/\s+/g, ' ').trim();

  // Baştaki başlık kalıpları. Türkçe "İ" harfi JavaScript'te büyük/küçük harf
  // duyarsız eşleşmede "i" ile EŞLEŞMEZ, bu yüzden harf sınıfları açıkça
  // yazılıyor — aynı tuzak daha önce kategori tespitinde de yaşanmıştı.
  text = text.replace(/^(ürün\s+)?a[çc][ıi]klamas[ıi]\s*[:：]\s*/i, '');
  text = text.replace(/^a[çc][ıi]klama\s*[:：]\s*/i, '');
  text = text.replace(/^[İIiı]çerik\s*[:：]\s*/, '');
  text = text.replace(/^.{0,60}?ned[iıİI]r\s*\??\s*[:：]\s*/i, '');

  // Metin ürün adını tekrarlıyorsa at — ad zaten başlıkta var.
  const name = productName.replace(/ /g, ' ').replace(/\s+/g, ' ').trim();
  if (name && text.toLocaleLowerCase('tr').startsWith(name.toLocaleLowerCase('tr'))) {
    const stripped = text.slice(name.length).replace(LEADING_PUNCTUATION, '');
    // Adı atmak cümleyi ortasından kesiyorsa vazgeç: "GI+ ürünü; lif..."
    // metninden "ürünü; lif..." gibi küçük harfle başlayan bir parça kalıyordu.
    if (stripped && stripped[0] === stripped[0].toLocaleUpperCase('tr')) {
      text = stripped;
    }
  }

  text = text.replace(LEADING_PUNCTUATION, '').trim();
  if (text.length < 30) return null;

  // İlk cümle; en az 40 karakter olsun ki "Nedir." gibi bir parça kalmasın.
  const sentence = /^(.{40,}?[.!?])(\s|$)/.exec(text);
  const intro = sentence ? sentence[1] : text;

  if (intro.length <= 120) return intro;
  return intro.slice(0, 117).replace(/\s+\S*$/, '') + '…';
}

/**
 * Arama sonucunda kırpılmayan bir başlık üretir.
 *
 * Google başlığı ~60-70 karakterde kesiyor. Daha da önemlisi: aşırı uzun
 * başlıklarda Google başlığı tamamen kendi yeniden yazıyor, yani kontrolü
 * kaybediyoruz. Denetimde 37 sayfanın 7'si 70 karakteri aşıyordu; en uzunu
 * 118 karakterdi (uzun ürün adları yüzünden).
 *
 * Öncelik sırası: (1) ürün adı tam sığıyorsa marka kuyruğuyla birlikte
 * kullan, (2) sığmıyorsa marka kuyruğunu at, (3) ürün adı tek başına bile
 * uzunsa kelime sınırından kırp. Ürün adı her zaman başta kalıyor çünkü
 * aramada görünen ve tıklamayı belirleyen kısım orası.
 */
export function buildPageTitle(subject: string, suffix: string, tail: string): string {
  const MAX = 65;
  const full = `${subject} ${suffix} | ${tail}`;
  if (full.length <= MAX) return full;

  const withoutTail = `${subject} ${suffix}`;
  if (withoutTail.length <= MAX) return withoutTail;

  // Kelime ortasından kesmemek için son boşluğa kadar geri git.
  const room = MAX - suffix.length - 2;
  const trimmed = subject.slice(0, Math.max(20, room)).replace(/\s+\S*$/, '');
  return `${trimmed}… ${suffix}`;
}

/** Meta açıklamayı Google'ın kestiği sınırın altında tutar. */
export function clampDescription(text: string, max = 155): string {
  if (text.length <= max) return text;
  return text.slice(0, max).replace(/\s+\S*$/, '') + '…';
}

/**
 * Başlığı arama sonucunda kırpılmayacak uzunlukta tutar. Kelime ortasından
 * kesmez. `buildPageTitle`'ın aksine yapıyı bilmediği için marka kuyruğunu
 * koruyamaz — bu yüzden yalnızca son çare güvenlik ağı olarak kullanılıyor.
 */
export function clampTitle(text: string, max = 65): string {
  if (text.length <= max) return text;
  return text.slice(0, max).replace(/\s+\S*$/, '') + '…';
}
