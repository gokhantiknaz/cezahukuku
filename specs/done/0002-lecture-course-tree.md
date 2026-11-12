# Spec 0002 — Ders notlarında ders ve başlık ağacı

- Status: Shipped
- Mode: lite
- Plan: `specs/plans/0002-plan.md`
- Self-critique: done

## Intent
Ders notları bugün düz bir liste. Yönetici, notların bir ders kitabı gibi numaralı başlık ağacı (müfredat) olarak
düzenlenmesini istiyor: ders → bölüm → başlık → alt başlık (örnek: "Ceza Hukuku Ders Notları — Başlıklar" PDF'i; 3 bölüm,
~250 başlık, 6 düzeye kadar). Her başlık kendi metnini, belgelerini ve madde bağlantısını taşıyabilir; okuyucu kanun okur
gibi soldaki içindekilerden başlığa gider ve sırayla ilerler. Başarı: yönetici bir ders açıp başlıklarını tek tek
kurabilir, düzenleyebilir, taşıyabilir; okuyucu ağaçta gezinir ve başlığı aramada bulur; mevcut düz ders notları aynen
erişilebilir kalır. Bilinçli olarak bu spec'te yapılmayanlar: başlıkları toplu içe aktarma (spec 0003), mevcut düz notları
ağaca taşıma (spec 0004).

## Requirements
- R-1: Birden çok **ders** olabilir (ad, isteğe bağlı kısa açıklama, görünürlük, sıra). Ders notları sayfası dersleri
  sırasıyla listeler; mevcut düz ders notları ayrı bir bölümde ("Diğer ders notları") ve eski adresleriyle açılmaya devam eder.
- R-2: Her dersin altında **başlık ağacı** vardır; en az 6 düzey desteklenir. Her başlığın adı vardır; metni, belgeleri
  (bugünkü ders notu belgeleriyle aynı türler ve sınırlar) ve grup/kanun/madde bağlantısı isteğe bağlıdır.
- R-3: Başlık **numaraları** ağaçtaki sıradan kendiliğinden hesaplanır ("1.5.1.1.3.1"); birinci düzey "1. Bölüm" olarak
  gösterilir. Başlık taşınınca kendisinin ve alt başlıklarının numarası güncellenir; numara elle girilmez.
- R-4: **Okuma ekranı:** solda dersin içindekileri (numaralı, açılır-kapanır; açık başlığın üst başlıkları açık, açık başlık
  vurgulu); ortada ders yolu (breadcrumb), numara ve başlık, metin, belgeler, alt başlıkların listesi; önceki/sonraki başlık
  (içindekilerdeki sırayla). İçeriği olmayan başlıkta "Bu başlığa henüz içerik girilmedi." ve alt başlıkları görünür.
  Telefonda içindekiler çekmecede açılır.
- R-5: Her dersin ve başlığın kalıcı, okunabilir bir adresi vardır; ders ya da başlık adı (ve ders açıklaması) değişse de
  adres değişmez. Aynı ders içinde aynı adlı başlıklar (ör. birçok "Genel Olarak") ayrı adres alır.
- R-6: **Görünürlük** ders ve başlık bazında "herkese açık / üyelere özel"dir ve üstten miras alınır (kanun → madde gibi):
  üyelere özel ders ya da başlık ve tüm altı, giriş yapmamış okuyucu için yoktur (adres "bulunamadı", içindekilerde ve
  aramada görünmez); giriş yapmış üye görür. Yönetici ve üye, özel ders/başlığı içindekilerde ve sayfasında "Özel"
  rozetiyle görür.
- R-7: **Yönetim (yalnız yönetici):** ders ekle/düzenle/sil; başlık ekle (kök ya da seçilen başlığın altına), düzenle,
  taşı (aynı düzeyde yukarı/aşağı; başka bir başlığın altına ya da kök düzeye), sil. Bir başlık kendi alt başlıklarından
  birinin altına taşınamaz. Alt başlığı olan başlık ve başlığı olan ders silinemez (önce alt başlıklar taşınır ya da
  silinir); silme onay ister ve kayıt saklanır (okuyucudan kalkar).
- R-8: **Arama:** başlık adı, metni ve belgelerinin okunabilen metni aranır; sonuç numaralı başlığı ve ders yolunu gösterir,
  tıklayınca başlığa (belgeyse o belgeye) gider.
- R-9: Bir maddeye bağlı başlık, o maddenin Açıklama sekmesindeki "Ders notları" listesinde numarası ve adıyla görünür ve
  başlığa gider (bugünkü davranış başlıklara genişler).

## User experience
- Flow (okuyucu): üst menü "Ders Notları" → dersler listesi → ders → içindekilerden başlık → metin/belgeler → önceki/sonraki.
- Flow (yönetici): dersler listesinde "Yeni ders" → ders sayfasında "Başlık ekle" → içindekilerde ya da başlık sayfasında
  başlığın "Alt başlık ekle / Düzenle / Taşı / Sil" işlemleri.
- States:
  - Dersler listesi: ders yok (yöneticiye "Yeni ders", okuyucuya "Henüz ders eklenmedi."); dersler var; "Diğer ders notları".
  - Ders sayfası: başlığı yok ("Bu derse henüz başlık eklenmedi."); başlıklar var (içindekiler + dersin açıklaması).
  - Başlık sayfası: içerikli; içeriksiz ("henüz içerik girilmedi" + alt başlıklar); belgeli; maddeye bağlı.
  - Bulunamadı (silinmiş, yanlış adres ya da anonime özel): mevcut "bulunamadı" sayfası.
  - Yönetici işlemleri: kaydederken düğme pasif; doğrulama hatası (ör. boş ad) alanda; taşıma geçersizse (kendi altına)
    mesaj; alt başlığı olan başlığı silme denemesinde gerekçeli mesaj; başarıda bildirim.
- Design reference: none — the flow above is the design. Görünüm kanun okuma ekranının üç panelli düzenini ve mevcut ders
  notu bileşenlerini (metin düzenleyici, belgeler) izler.
- Accessibility: içindekiler klavyeyle gezilebilir (açılır-kapanır düğmeler ve bağlantılar Tab ile; açık başlık
  `aria-current`); yönetici simge düğmelerinin erişilebilir adı başlığı içerir; 390 px'te yatay taşma yok.

## Constraints & out of scope
- Sensitive area: Yetki ve görünürlük (BR-1, BR-5).
- Split: proposed 0002 (ağaç, okuma, tek tek yönetim) · 0003 (başlıkları toplu içe aktarma — yapıştır/PDF — ve örnek
  PDF'in aktarılması) · 0004 (mevcut düz notları ağaca taşıma) — kabul: Gokhan, 2026-11-12.
- Out of scope: toplu içe aktarma (0003); düz notların ağaca taşınması (0004); sürükle-bırak ile taşıma (düğmelerle
  yapılır); numarayı elle girme; ders/başlık sürüm geçmişi.
- Mevcut düz ders notlarının adresleri ve davranışı değişmez.
- Performans: 300 başlığa kadar bir dersin içindekileri tek seferde gelir; ders ve başlık sayfaları sunucuda çizilir ve
  yerel ortamda 1 saniyenin altında açılır.

## Acceptance criteria
- [x] AC-1 — Yönetici ders oluşturur; Ders Notları sayfası dersleri sırasıyla listeler; mevcut düz ders notları "Diğer ders
  notları" bölümünde görünür ve eski adresleriyle açılır.
- [x] AC-2 — Yönetici kök düzeyde ve seçilen başlığın altına başlık ekler (en az 6 düzey); numaralar sıradan kendiliğinden
  gösterilir (ör. altıncı düzeyde "1.5.1.1.3.1"; birinci düzey "1. Bölüm"); aynı ders içinde aynı adlı başlıklar ayrı
  adreslerle açılır.
- [x] AC-3 — Yönetici bir başlığı aynı düzeyde yukarı/aşağı ve başka bir başlığın altına taşır; kendisinin ve alt
  başlıklarının numaraları güncellenir, adresleri değişmez; kendi alt başlığının altına taşıma reddedilir ve ağaç değişmez.
- [x] AC-4 — Başlığa metin, belge ve madde bağlantısı eklenip düzenlenebilir; içeriği olmayan başlık "Bu başlığa henüz
  içerik girilmedi." ve alt başlıklarını gösterir.
- [x] AC-5 — Okuma ekranında içindekiler açık başlığın üst başlıklarını açık ve açık başlığı vurgulu gösterir; ders yolu,
  önceki/sonraki başlık içindekilerdeki sırayla çalışır; telefonda içindekiler çekmecede açılır ve 390 px'te yatay taşma yok.
- [x] AC-6 — Üyelere özel ders ya da başlık ve tüm altı giriş yapmamış okuyucu için "bulunamadı" döner, içindekilerde ve
  aramada görünmez, önceki/sonraki geçişinde atlanır; giriş yapmış üye "Özel" rozetiyle görür; üstü özel olan başlık
  kendi ayarı açık olsa da özel sayılır.
- [x] AC-7 — Ders ve başlık ekleme, düzenleme, taşıma ve silme yalnız yöneticiye açıktır: giriş yok → 401, `standard` ve
  `reader` → 403, kayıt değişmez; okuyucu ve üyeler yönetim düğmelerini görmez.
- [x] AC-8 — Alt başlığı olmayan başlık ve başlığı olmayan ders onayla silinir ve belgeleriyle birlikte okuyucudan kalkar
  (kayıt ve dosyalar saklanır); alt başlığı olan başlık
  ya da başlığı olan ders silinemez, gerekçeli mesaj gösterilir.
- [x] AC-9 — Arama başlık adında, metninde ve belge metninde bulur; sonuç numaralı başlığı ve ders yolunu gösterir,
  tıklayınca başlığa gider; silinen başlık aramada çıkmaz.
- [x] AC-10 — Bir maddeye bağlı başlık, maddenin Açıklama sekmesindeki ders notları listesinde numarası ve adıyla görünür ve
  başlığa gider.

## Definition of Done
- [x] Every acceptance criterion mapped to proof (test or reproducible observation)
- [x] `scripts/check` green
- [x] Independent review done; real findings fixed, noise rejected with written rationale, deferred ones in `specs/backlog.md`
- [x] Docs / ADRs updated if behavior or architecture changed
- [x] Spec moved to `specs/done/` (it becomes immutable there)

## Scorecard (fill at ship — honest numbers make the process improvable)
| Metric | Value |
|---|---|
| Spec revisions | 0 onay sonrası (plan değişiklikleri A-1…A-7; BR-1 istisnası ADR 0001) |
| Fix rounds | 2 (fix-2 yalnız docs) |
| Review findings: real / noise / deferred | 9 / 1 / 1 (B-6) — VERIFY notları: N-2 kabul, N-4 noise, N-1 → B-7, N-3 → B-8 |
| Regressions introduced | 0 |
| Bugs escaped to production | 0 (henüz canlıya alınmadı) |
