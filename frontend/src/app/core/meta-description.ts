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
