# Spec 0004 — Ders başlıklarını toplu içe aktarma ve ders bölüm numarası

- Status: Approved
- Mode: lite
- Plan: `specs/plans/0004-plan.md`
- Self-critique: done

## Intent
Yönetici, yüzlerce başlıktan oluşan ders içindekilerini tek tek girmek yerine bir seferde aktarmak istiyor. Kaynak, ders
kitabının başlık listesi; örnek "Ceza Hukuku Ders Notları — Başlıklar" PDF'i: 3 bölüm, ~250 başlık, 6 düzeye kadar.
Yönetici her bölümü ayrı bir PDF olarak hazırlıyor ve her PDF ayrı bir ders oluyor ("Ceza Hukukuna Giriş", "Suç
Teorisi", "Yaptırım"). Başlık listesi yapıştırılır ya da dosyadan alınır; sistem düzeyleri numaralardan anlar,
ağacı önizlemede gösterir; yönetici hataları düzeltip onaylayınca yeni ders kurulur. Sitedeki numaralar PDF'tekiyle
birebir aynıdır ("2.1 Suçun Unsurları", "2.1.1 Tipiklik"). Bunun için derse isteğe bağlı bir **bölüm numarası**
verilir; numarasız dersler bugünkü gibi numaralanır. Başarı: bölüm PDF'i birkaç dakikada, numaraları PDF'le aynı bir
ders olarak sitede olur; yarım içerik anonim okuyucuya ve arama motorlarına düşmez. Bilerek yapılmayanlar: başlıkların
metinlerini aktarmak (yalnız adları gelir), var olan bir derse aktarmak ya da başlık başlık birleştirmek, mevcut düz
notları ağaca taşımak (spec 0005).

## Requirements
- R-1: **Ders bölüm numarası.** Yönetici ders eklerken ve düzenlerken derse isteğe bağlı bir bölüm numarası (1–99
  arası tam sayı) verebilir, değiştirebilir ya da kaldırabilir. Numaralı derste başlık numaraları onunla başlar:
  birinci düzey "N.1", "N.2", altı "N.1.1", "N.1.1.1"… Numarasız derste bugünkü gibidir (birinci düzey "1. Bölüm",
  altı "1.1"). Aynı numarayı birden çok ders taşıyabilir.
- R-2: Bölüm numarası her yerde aynı numarayı verir: içindekiler, ders sayfasındaki "Bölümler" listesi, başlık
  sayfası, ders yolu, önceki/sonraki bağlantıları, madde ekranındaki ders notu bağlantısı, arama sonuçları, taşıma
  bildirimi. Numara verilince, değişince ya da kaldırılınca hepsi hemen güncellenir; adresler değişmez. Numaralı ders,
  Ders Notları listesinde ve ders sayfasının başlığında "N. Bölüm — Ad" olarak görünür.
- R-3: **İçe aktarma girişi.** Ders Notları sayfasında yöneticiye "Yeni ders"in yanında **"Başlıkları içe aktar"**
  vardır. Açılan ekranda başlık listesi metin kutusuna yapıştırılır ya da dosyadan (PDF, Word .docx, .txt) alınır;
  dosyadan alınan metin aynı kutuya gelir ve düzenlenebilir.
- R-4: **Okuma kuralları** (satır satır):
  - Numaralı satır ("1.5.1. Başlık"; sondaki nokta isteğe bağlı): bir başlıktır; düzeyi numaranın parça sayısından
    anlaşılır. Satırdaki numara kaydedilmez; numarayı sistem ağaçtaki yerden hesaplar.
  - Bölüm satırı ("2. BÖLÜM – SUÇ TEORİSİ"; büyük/küçük harf fark etmez; ayraç tire, uzun tire ya da iki nokta):
    - Metinde **bir tane** varsa: ders adı önerisi bölümün adı, bölüm numarası önerisi bölümün numarasıdır; "2.1"
      gibi satırlar dersin birinci düzeyidir.
    - **Birden çok** varsa (bütün kitap tek metinde): ders numarasız kurulur; her bölüm satırı birinci düzey bir
      başlıktır ("1. Bölüm Ceza Hukukuna Giriş"), "1.1" onun altındadır. Numaralar yine PDF'le aynıdır.
    - **Hiç** yoksa: ders adı önerisi ilk numaralı satırdan önceki ilk dolu satırdır; bölüm numarası önerisi boştur.
  - Numarasız satır: ilk numaralı satırdan önceyse başlık bilgisidir (ad önerisi dışında yok sayılır); sonraysa bir
    önceki başlığın alt satıra kaymış devamıdır ve adına boşlukla eklenir. Yalnız sayıdan oluşan satır (sayfa
    numarası) ve boş satır yok sayılır.
  - Tamamı büyük harfle yazılmış ad Türkçe kurala göre her kelimenin ilk harfi büyük olacak biçimde düzeltilir
    ("CEZA HUKUKUNA GİRİŞ" → "Ceza Hukukuna Giriş"; bağlaçlar "ve, veya, ile, ya, da, de, ki" ilk kelime değilse
    küçük). Karışık harfli adlar olduğu gibi kalır. Tamamı büyük harfli addaki kısaltma da küçülür ("TCK" → "Tck");
    önizlemede görünür, metinde düzeltilir.
- R-5: **Önizleme.** "Önizle" ağacı sistemin hesaplayacağı numaralarla (bölüm numarası dahil), toplam başlık
  sayısıyla, hatalar ve uyarılarla gösterir; hata ve uyarılar kaynak metnin satır numarasını verir. Önizleme hiçbir
  şey kaydetmez. Metin, ders adı ya da bölüm numarası değişince önizleme "güncel değil" olur ve kayıt yeniden
  önizlenene kadar kapanır. Başlık düzeltmeleri metin kutusunda yapılır.
  - **Hata** (kayıt kapalı): atlanan düzey (ör. 1.5'ten sonra 1.5.1.1; ya da bölüm numarası yokken "2.1" ile
    başlayan metin); bölüm numarası verilmişken başka numarayla başlayan satır; hiç başlık yok; 300'den fazla
    başlık; 300 karakterden uzun ad; boş ders adı; 200.000 karakterden uzun kaynak metin.
  - **Uyarı** (kayıt açık): satırdaki numara sistemin vereceğinden farklı (ör. 1.3'ten sonra 1.5; ağaç satır
    sırasıyla kurulur); aynı üst başlık altında aynı ad (örnek PDF'te 2.9.1 ve 2.9.3 "Genel Olarak").
- R-6: Formda ders adı ve bölüm numarası önerilerle dolu gelir ve değiştirilebilir; görünürlük seçilir, varsayılan
  **özel** (yalnız giriş yapanlar).
- R-7: **Kaydet** tek seferde ve ya hep ya hiç çalışır: yeni ders Ders Notları listesinin sonunda, bütün başlıklar
  metindeki sırayla kurulur. Başarıda yeni dersin sayfasına gidilir ve "Ders kuruldu; N başlık aktarıldı." bildirimi
  çıkar. Hata olursa hiçbir ders ya da başlık oluşmaz; gerekçeli mesaj gösterilir, form ve metin korunur.
- R-8: **Görünürlük (hassas alan):** aktarılan başlıkların kendi ayarı açıktır; etkin görünürlükleri dersten gelir
  (spec 0002 kuralları). Özel derste başlıklar giriş yapmamış okuyucuya okumada, içindekilerde, madde ekranında,
  aramada ve site haritasında görünmez.
- R-9: **Yetki (hassas alan):** önizleme ve kayıt yalnız yöneticiye açıktır; diğer kullanıcılar "Başlıkları içe
  aktar"ı görmez, ekranın adresine giderse içe aktarma yapamaz.
- R-10: Mevcut dersler, başlıklar ve düz ders notları bu işten etkilenmez (bölüm numarası verilmedikçe görünümleri
  aynıdır).

## User experience
- Flow: yönetici Ders Notları sayfasında "Başlıkları içe aktar"a basar → başlık listesini yapıştırır ya da dosya
  seçer (metin kutuya gelir) → "Önizle" → ağacı, hataları ve uyarıları görür → gerekirse metni düzeltip yeniden
  önizler → ders adını, bölüm numarasını ve görünürlüğü kontrol eder → "Dersi kur" → yeni dersin sayfası ve bildirim.
  Bölüm numarası ayrıca ders ekleme/düzenleme penceresinde verilir.
- States:
  - Boş: metin kutusu boş; "Önizle" kapalı.
  - Dosya okunuyor: dosya düğmesi beklemede.
  - Dosya okunamadı: desteklenmeyen tür, eski .doc ya da metni olmayan (taranmış) PDF → gerekçeli hata; kutu
    değişmez.
  - Önizleme hatalı: ağaç + hata listesi; "Dersi kur" kapalı.
  - Önizleme temiz (uyarılı ya da uyarısız): ağaç + varsa uyarılar; "Dersi kur" açık.
  - Önizleme güncel değil: metin/ad/numara değişti; "Dersi kur" kapalı, "Önizle" ile yenilenir.
  - Kaydediliyor: "Dersi kur" beklemede; form kilitli.
  - Başarı: yeni dersin sayfası + bildirim. Kayıt hatası: gerekçeli mesaj, form ve metin aynen.
  - Yetkisiz: düğme yok; ekranın adresine giden yönetici olmayan kullanıcı içe aktarma yapamaz (bulunamadı ya da
    giriş sayfası).
- Design reference: none — the flow above is the design. Görünüm kanun içe aktarma ekranını (yapıştır → önizle →
  içe aktar) ve mevcut ders yönetimi pencerelerini izler.
- Accessibility: metin kutusu, dosya düğmesi, ders adı, bölüm numarası ve görünürlük etiketli; hata ve uyarı listesi
  önizlemeden sonra ekran okuyucuya bildirilir; bütün akış klavyeyle yapılır; 390 px'te yatay taşma yok.

## Constraints & out of scope
- Sensitive area: Yetki ve görünürlük (BR-1, BR-5) — yeni yönetici işlemi; yeni dersin ve başlıkların görünürlüğü.
- Numaralar ADR 0001'e uyar (okuyucudan bağımsız, ağaçtan hesaplanır); bölüm numarası yalnız numaranın önüne gelen
  kısmı belirler.
- Performans (yerel): 300 başlıklı metnin önizlemesi 2 sn, kaydı 5 sn altında.
- Dosyadan metin alma, kanun içe aktarmadaki dosya türleri ve sınırlarıyla aynıdır (.docx, .pdf, .txt; .doc ve
  diğerleri gerekçeli hata).
- Split: proposed 0004 (ders bölüm numarası, R-1/R-2) · 0005 (başlıkları toplu içe aktarma); kept as one — Gokhan,
  2026-11-13, kriter sayısı sınırın içinde ve içe aktarmanın amacı PDF'le aynı numara.
- Aynı adlı ders varken yeni dersin adresi ek alır (ör. "suc-teorisi-2"); temiz adres için eski ders önce silinir.
  Silinmiş dersin adresinin yeniden kullanılabilirliğini plan doğrular; kullanılamıyorsa kullanıcıya bildirilir.
- Out of scope: başlık metinlerini (içerik) ya da belgeleri aktarma; var olan derse aktarma, ağacı değiştirme ya da
  başlık başlık birleştirme; tek seferde birden çok ders kurma; taranmış PDF için OCR (Faz 3); önizlemede satır satır
  düzenleme (düzeltme metin kutusunda); B-6 (belgeli derste kayıt süresi) düzeltmesi — ayrı iş; düz notları ağaca
  taşıma (spec 0005).

## Acceptance criteria
- [ ] AC-1 — Derse bölüm numarası 2 verilince başlık numaraları "2.1", "2.1.1"… olur: içindekiler, ders sayfası
  "Bölümler", başlık sayfası, ders yolu, önceki/sonraki, madde ekranındaki bağlantı, arama ve taşıma bildirimi aynı
  numarayı gösterir; ders listede ve ders sayfasında "2. Bölüm — Ad" görünür. Numara kaldırılınca bugünkü biçime
  ("1. Bölüm", "1.1") döner; adresler değişmez. Numarasız dersler değişmez.
- [ ] AC-2 — Örnek PDF'in tek bölümlük kısmı (ör. "2. BÖLÜM – SUÇ TEORİSİ" ve altı) aktarılınca ad önerisi "Suç
  Teorisi", bölüm numarası önerisi 2 olur; önizlemedeki her numara PDF'tekiyle aynıdır (2.1 … 2.9.4.2.2); sayfa
  numarası satırları yok sayılır; başlık sayısı PDF'teki satır sayısına eşittir.
- [ ] AC-3 — Örnek PDF'in tamamı (üç bölüm satırı) tek metin olarak aktarılınca numarasız bir ders önerilir; üç
  bölüm birinci düzeydir ("1. Bölüm Ceza Hukukuna Giriş"), bütün numaralar PDF'le aynıdır; tamamı büyük harfli adlar
  R-4'e göre düzeltilir, karışık harfli adlar değişmez.
- [ ] AC-4 — Dosyadan alma: .pdf, .docx ve .txt dosyasının metni kutuya gelir; .doc, desteklenmeyen tür ve metni
  olmayan PDF gerekçeli hata verir, kutu değişmez.
- [ ] AC-5 — Hatalar satır numarasıyla gösterilir ve kaydı kapatır: atlanan düzey, bölüm numarası varken başka
  numarayla başlayan satır, hiç başlık yok, 300'den fazla başlık (300 geçer, 301 hata), 300 karakterden uzun ad,
  boş ders adı, 200.000 karakterden uzun metin; düzeltilip yeniden önizlenince kayıt açılır. Uyarılar (numara farkı, aynı adlı kardeş) kaydı
  kapatmaz. Numarasız satır önceki başlığın adına eklenir. Önizlemeden sonra metin/ad/numara değişince kayıt kapanır.
- [ ] AC-6 — "Dersi kur" yeni dersi listenin sonunda, başlıkları metindeki sırayla kurar; yeni dersin sayfasına
  gidilir, bildirim başlık sayısını verir. Kayıt sunucuda başarısız olursa hiçbir ders ya da başlık oluşmaz; form ve
  metin korunur. 300 başlıklı kayıt yerelde 5 sn altında, önizlemesi 2 sn altında.
- [ ] AC-7 — Görünürlük (hassas): görünürlük varsayılanı özeldir. Özel kurulan dersin başlıkları giriş yapmamış
  okuyucuya okumada 404, içindekiler, madde ekranı, arama ve site haritasında yoktur; üye görür. Açık seçilirse herkes
  görür. Aktarılan başlıkların kendi ayarı açıktır.
- [ ] AC-8 — Yetki (hassas): önizleme ve kayıt yalnız yöneticiye açıktır — giriş yok 401, `standard` ve `reader` 403
  ve hiçbir şey oluşmaz; "Başlıkları içe aktar" yalnız yöneticide görünür; önizleme hiçbir kayıt oluşturmaz. Bölüm
  numarasını da yalnız yönetici verir.
- [ ] AC-9 — Erişilebilirlik ve telefon: bütün akış klavyeyle yapılır; alanlar etiketli; hata/uyarı listesi ekran
  okuyucuya bildirilir; 390 px'te yatay taşma yok.

## Definition of Done
- [ ] Every acceptance criterion mapped to proof (test or reproducible observation)
- [ ] `scripts/check` green
- [ ] Independent review done; real findings fixed, noise rejected with written rationale, deferred ones in `specs/backlog.md`
- [ ] Docs / ADRs updated if behavior or architecture changed
- [ ] Spec moved to `specs/done/` (it becomes immutable there)

## Scorecard (fill at ship — honest numbers make the process improvable)
| Metric | Value |
|---|---|
| Spec revisions | |
| Fix rounds | |
| Review findings: real / noise / deferred | |
| Regressions introduced | |
| Bugs escaped to production | |
