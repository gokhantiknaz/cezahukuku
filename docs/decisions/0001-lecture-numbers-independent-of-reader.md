# ADR 0001 — Ders başlığı numaraları okuyucudan bağımsızdır

- Status: Accepted
- Date: 2026-11-12

## Context
Spec 0002 ile ders notları numaralı bir başlık ağacına dönüştü ("1.5.1.1.3.1"; birinci düzey "1. Bölüm"). Bir ders
ya da başlık "üyelere özel" olabilir ve bu, altındaki bütün başlıklara miras kalır; giriş yapmamış okuyucu bu
başlıkları içindekilerde, sayfada ve aramada hiç görmez (BR-1).

Numara iki türlü hesaplanabilir: ağacın tamamından (her okuyucu için aynı) ya da okuyucunun görebildiği başlıklardan.
Ağacın tamamından hesaplanırsa, araya üyelere özel bir başlık giren yerde anonim okuyucu numara boşluğu görür
("1. Bölüm, 3. Bölüm"). Bu, orada bir şey olduğunu (adı ve içeriği değil) belli eder. `docs/domain.md` BR-1 ise
private içeriğin "varlığı"nın listede ve aramada sızmamasını ister. Bağımsız inceleme (spec 0002, review round 1,
M-1) bu çelişkiyi buldu; plan değişikliği A-7 numaraların okuyucudan bağımsız olmasını zaten onaylamıştı.

Kullanıcılar hukukçu: ders notlarına numarayla atıf yapar ("1.4.1.2'de anlatıldığı gibi"), notları paylaşır,
yazdırır.

## Decision
Başlık numarası ağacın tamamından, okuyucudan bağımsız hesaplanır; aynı numara herkes için aynı başlıktır. Gizli
başlığın bıraktığı numara boşluğu BR-1 anlamında "varlık sızıntısı" sayılmaz: adı, içeriği, belgeleri, adresi ve
sayısı dışında hiçbir bilgisi okuyucuya verilmez. BR-1 bu istisnayla açıklanmıştır (`docs/domain.md`).

## Consequences
- Kazanç: numaralar kalıcıdır; giriş yapmış ve yapmamış okuyucu aynı numarayla aynı başlığı anlar, atıflar kaymaz;
  bir başlığın gizlenmesi ya da açılması başka başlıkların numarasını değiştirmez.
- Bedel: anonim okuyucu numara boşluğundan gizli başlık bulunduğunu ve kabaca kaç tane olduğunu çıkarabilir.
  Ad, içerik ve adres sızmaz. Bu istisna yalnız ders başlığı numaraları içindir; başka hiçbir yerde BR-1 gevşemez.
- Yönetici gizli başlığı listenin sonuna taşıyarak boşluğu görünmez kılabilir (sonda boşluk oluşmaz).

## Alternatives considered
- **Numaraları okuyucuya göre hesaplamak:** BR-1'i harfiyen karşılar, ama anonim okuyucunun "2. Bölüm"ü üyenin
  "3. Bölüm"ü olur; paylaşılan ya da yazdırılan atıflar okuyucuya göre başka başlığı gösterir. Hukuk metninde
  numaranın kimlik işlevi daha değerli bulundu.
- **Gizli başlıkları numaralamamak (sona ya da ayrı dizi):** özel başlıkların ağaçtaki yerini bozar; bir başlığın
  görünürlüğü değişince numaralar yine kayar.

## Revisit triggers
- Gizli başlık sayısının ya da konumunun kendisinin hassas olduğu bir içerik türü çıkarsa.
- Anonim okuyucudan numara boşluğuyla ilgili şikâyet gelirse.
- Numarayı elle girme (spec 0002'de kapsam dışı) istenirse.
