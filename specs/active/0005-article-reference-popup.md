# Spec 0005 — Metindeki "(madde X, Y)" atıflarını işaretleyip açılır pencerede gösterme

- Status: In progress
- Mode: lite
- Plan: `specs/plans/0005-plan.md`
- Self-critique: done

## Intent
Hukukçu bir kanun maddesini ya da ders notunu okurken metinde "(madde 21, 22)" gibi parantez içi atıflarla
karşılaşıyor. Bugün anılan maddeyi görmek için sayfadan ayrılması gerekiyor. Bu iş tamamlandığında bu atıflar
metinde mavi işaretlenir. Okuyucu bir numaraya tıklayınca o maddenin metni okuduğu yerin üstünde açılan bir
pencerede gösterilir. Okuyucu yerini kaybetmez. Öncelik **kanun maddelerinin metnidir**, ders notları da aynı
davranışı kazanır. Atıflar metinden kendiliğinden tanınır; yönetici bağlantı girmez. Bilerek yapılmayanlar:
gerekçe, açıklama, içtihat gibi editoryal sekmelerde ve kişisel notlarda işaretleme; kanun adı geçen biçimler
("(TCK m. 81)"); aralıklar ("(madde 21-23)"); atıfları elle düzeltme.

## Requirements
- R-1: **Nerede.** İşaretleme iki yerde yapılır:
  - kanun maddesinin okuma ekranındaki madde metni (fıkralar ve bentler);
  - ders notunun gövdesi, yani düz ders notu sayfası ve ders başlığı sayfası.
  Yönetim ekranlarında (düzenleme formları, panel) işaretleme yapılmaz.
- R-2: **Tanınan biçimler.** Atıf **parantez içinde** ve kanun adı geçmeden yazılmış olmalıdır. "madde", "maddeler",
  "m." ya da "md." ile başlar, ardından bir ya da birden çok madde numarası gelir; numaralar virgül ya da "ve" ile ayrılır.
  Büyük/küçük harf ve boşluk farkı önemsizdir. Örnekler: "(madde 21)", "(Madde 21, 22)", "(madde 21 ve 22)",
  "(maddeler 21, 22)", "(m. 21)", "(md.21)". Numaranın ardındaki fıkra eki ("21/2") tanınır ama pencere maddenin tamamını açar.
  Parantez dışındaki yazımlar ("madde 21'e göre"), aralıklar ("21-23"), kanun adı geçen biçimler ve "geçici/ek madde"
  işaretlenmez.
- R-3: **Hangi kanun.**
  - Madde metninde: atıf maddenin kendi kanunudur.
  - Ders notunda: not bir kanuna ya da maddeye bağlıysa o kanundur. Hiçbir şeye bağlı değilse ya da yalnız bir kanun
    grubuna bağlıysa TCK'dır (5237 sayılı Türk Ceza Kanunu); grupta birden çok kanun olduğu için grup kanunu
    belirlemez.
  - O kanunda o numaralı madde yoksa numara işaretlenmez.
- R-4: **İşaretleme.** Atıftaki her numara ayrı işaretlenir ve ayrı tıklanır: "(madde **21**, **22**)". İşaret
  maviyle yapılır. Kişisel vurgu renklerinden, özellikle sarıdan ayırt edilmelidir. Metnin kendisi değişmez:
  kopyalama, arama ve kişisel vurgu bugünkü gibi çalışır.
- R-5: **Pencere.** Numaraya tıklanınca (ya da klavyeyle odaklanıp Enter'a basılınca) okuma ekranının üstünde bir
  pencere açılır. Pencerede şunlar bulunur:
  - kanunun kısaltması ya da adı, madde numarası ve başlığı;
  - maddenin metni, fıkra ve bentleriyle. Hangi metnin gösterileceği:
    - madde ekranında okuyucu bir suç tarihi seçmişse (`?tarih=`) **o tarihte** yürürlükte olan metin; tarih
      seçmemişse bugün yürürlükte olan metin;
    - ders notunda bugün yürürlükte olan metin;
  - madde (o tarihte) mülgaysa "Mülga" ibaresi;
  - maddenin kendi sayfasına giden **"Maddeye git"** bağlantısı. Bağlantı, seçili suç tarihini de taşır.
  Pencerede gösterilen metindeki "(madde X)" atıfları işaretlenmez: pencere içinden yeni pencere açılmaz.
  Pencere Esc tuşuyla, kapat düğmesiyle ve pencere dışına tıklanınca kapanır. Kapanınca odak tıklanan numaraya döner.
  Okuma ekranının kaydırma konumu değişmez.
- R-6: **Görünürlük (hassas alan).** Okuyucunun göremediği madde, örneğin giriş yapmamış okuyucu için özel (private)
  bir kanunun maddesi, kütüphanede hiç olmayan madde gibi davranır: numara işaretlenmez ve pencere açılmaz. Sayfa
  kaynağında da maddenin varlığına, metnine ya da başlığına dair bir iz kalmaz (BR-1).
- R-7: Mevcut davranış korunur: madde metnindeki otomatik atıf bağlantıları ("13 üncü maddede"), kişisel vurgu ve
  notlar, değişiklik işaretleri ve ders notu görünümü bugünkü gibi çalışır.

## User experience
- Flow: okuyucu madde ya da ders notu sayfasını açar → metinde mavi işaretli "(madde 21, 22)" atıfını görür → "21"e
  tıklar → pencerede TCK 21'in başlığını ve metnini (seçili suç tarihine göre, yoksa bugünkü) okur → Esc ile kapatır ve okumaya kaldığı yerden devam
  eder, ya da "Maddeye git" ile maddenin sayfasına geçer.
- States:
  - İşaretli atıf: mavi, tıklanabilir. İmleç el olur, üstüne gelince başlık ipucu görünmez (bilgi pencerede).
  - Pencere yükleniyor: pencere açık, içerik yerinde yükleniyor göstergesi.
  - Pencere dolu: başlık, metin, "Maddeye git".
  - Mülga madde: "Mülga" ibaresi ve "Maddeye git".
  - Yükleme hatası (bağlantı koptu, sunucu hatası): pencerede "Madde yüklenemedi" ve "Yeniden dene".
  - İşaretlenmeyen atıf (madde yok, görünmüyor, kanun belirlenemedi): düz metin.
- Design reference: yok, yukarıdaki akış tasarımın kendisidir.
- Accessibility: işaretli numara klavyeyle odaklanabilir (Tab) ve Enter ya da Space ile açılır. Erişilebilir adı
  "TCK madde 21'i aç" biçimindedir. Pencere iletişim kutusudur: odak pencereye taşınır ve pencerede kalır, Esc
  kapatır, odak geri döner. Mavi işaret açık ve koyu temada okunur kontrastta olur. Mobilde pencere ekrana sığar ve
  kendi içinde kayar.

## Constraints & out of scope
- Sensitive area: Yetki ve görünürlük (R-6, BR-1).
- Sensitive area: Mevzuat metninin doğruluğu (R-5, BR-3): pencere, suç tarihine göre doğru sürümü gösterir.
- Split: proposed 0005 madde metni / 0006 ders notu; kept as one — Gokhan, 2026-11-16, tek kullanıcı yeteneği ve
  aynı tanıma kuralı; bölünse pencere ve görünürlük ölçütleri iki spec'te tekrarlanırdı.
- Pencere, okuyucunun normalde maddeye gittiğinde göreceğinden fazlasını göstermez. Görünürlük kuralı madde
  sayfasıyla aynıdır.
- Out of scope: gerekçe, açıklama, genel gerekçe, içtihat, norm denetimi sekmeleri (B-13); kişisel notlar ve vurgu notları;
  haberler ve test soruları; ders notuna ekli belgeler (PDF ve benzeri); kanun adı geçen biçimler ("(TCK m. 81)",
  "(5271 s. K. m. 231)"); aralıklar ("21-23"); geçici ve ek maddeler; atıfların elle düzeltilmesi; pencerede
  vurgu yapma ya da not alma.

## Acceptance criteria
- [ ] AC-1 — Bir TCK maddesinin metnindeki "(madde 21, 22)" ifadesinde "21" ve "22" ayrı ayrı mavi işaretlidir.
  "21"e tıklanınca TCK 21'in başlığı ve bugünkü metni pencerede açılır, okuma ekranı yerinde kalır.
- [ ] AC-2 — Tanınan biçimler işaretlenir: "(madde 21)", "(Madde 21 ve 22)", "(maddeler 21, 22)", "(m. 21)", "(md.21)",
  "(madde 21/2)".
  Tanınmayanlar işaretlenmez: "madde 21'e göre" (parantezsiz), "(madde 21-23)", "(TCK m. 21)", "(geçici madde 1)".
- [ ] AC-3 — Madde metnindeki atıf maddenin kendi kanununa gider. Örneğin CMK maddesindeki "(madde 231)" CMK
  231'i açar.
- [ ] AC-4 — Ders notunda kanun seçimi doğrudur: CMK'ya bağlı notta "(madde 231)" CMK 231'i açar; hiçbir şeye
  bağlı olmayan notta ve yalnız bir kanun grubuna bağlı notta "(madde 21)" TCK 21'i açar. Kural hem düz ders notunda hem ders başlığında geçerlidir.
- [ ] AC-5 — Kanunda olmayan numara ("(madde 999)") işaretlenmez, düz metin kalır.
- [ ] AC-6 — Giriş yapmamış okuyucu, herkese açık bir ders notunda özel bir kanunun maddesine yapılan atıfı
  işaretsiz görür. Sayfa kaynağında o maddenin başlığı ya da metni bulunmaz. Giriş yapmış okuyucu aynı atıfı
  işaretli görür ve açabilir.
- [ ] AC-7 — Mülga maddeye yapılan atıf işaretlidir; pencerede "Mülga" görünür.
- [ ] AC-8 — Pencere Esc, kapat düğmesi ve dışarı tıklama ile kapanır; kapanınca odak tıklanan numaraya döner.
  "Maddeye git" maddenin sayfasını açar. Numara Tab ile odaklanıp Enter ile açılabilir.
- [ ] AC-9 — Pencere yüklenemezse "Madde yüklenemedi" ve "Yeniden dene" görünür; "Yeniden dene" başarılı olunca
  madde gösterilir. Yüklenirken bekleme göstergesi görünür.
- [ ] AC-10 — Mevcut davranış bozulmaz: madde metnindeki otomatik atıf bağlantıları, işaretli atıfın üstünden
  geçen kişisel vurgu, değişiklik işaretleri ve "Kopyala" bugünkü gibi çalışır; kopyalanan metin özgün metinle
  aynıdır.
- [ ] AC-11 — Yönetim ekranlarında (madde düzenleme, ders notu düzenleme) atıflar işaretlenmez.
- [ ] AC-12 — Dar ekranda (telefon genişliği) pencere ekrana sığar, uzun madde metni pencerenin içinde kayar.
- [ ] AC-13 — Suç tarihi: madde ekranında, atıf yapılan maddenin değişmeden önceki sürümünün yürürlükte olduğu bir
  tarih seçilmişse (`?tarih=`) pencere o eski metni gösterir. Tarih seçilmemişse ya da ders notundaysa bugünkü
  metni gösterir. O tarihte mülga olan madde için "Mülga" görünür. "Maddeye git" aynı tarihle açılır.
- [ ] AC-14 — Pencerede gösterilen madde metnindeki "(madde X)" atıfları işaretli değildir ve tıklanmaz.

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
