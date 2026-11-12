# Spec 0003 — Ders içindekilerinde sürükle-bırak ile başlık taşıma

- Status: In progress
- Mode: lite
- Plan: `specs/plans/0003-plan.md`
- Self-critique: done

## Intent
Yönetici, ders başlıklarının yerini "Taşı" penceresi ve yukarı/aşağı düğmeleriyle tek tek değiştirmek yerine,
içindekilerde başlığı fareyle sürükleyip bırakarak düzenlemek istiyor; ~250 başlıklı derslerde bu çok daha hızlı.
Başlık alt başlıklarıyla birlikte taşınır; numaralar ("1.4.2") hemen yeniden hesaplanır ve her yerde güncellenir
(içindekiler, başlık sayfası, ders yolu, madde ekranı, arama); adresler değişmez. Başarı: yönetici bir başlığı aynı
düzeyde, başka bir başlığın altına ya da birinci düzeye tek hareketle taşır; geçersiz taşıma (kendi altına) yapılamaz;
okuyucu bir şey fark etmez, yalnız yeni sırayı görür. Bilinçli olarak yapılmayanlar: derslerin kendi sırası, birden
çok başlığı birlikte taşıma, telefonda sürükleme, geri al düğmesi.

## Requirements
- R-1: Sürükle-bırak yalnız yöneticiye ve yalnız dersin içindekilerinde (sol panel) vardır; geniş ekranda fareyle
  çalışır. Dar ekranda ve telefon çekmecesindeki içindekilerde "Sırala" hiç görünmez. Ders sayfasındaki "Bölümler" ve başlık sayfasındaki "Alt başlıklar" listeleri değişmez.
- R-2: İçindekilerde yöneticiye bir **"Sırala"** düğmesi vardır. Sürükleme yalnız bu mod açıkken çalışır; mod
  kapalıyken içindekiler bugünkü gibidir (başlıklar bağlantıdır). Mod açıkken düğme "Bitti" olur; mod, sayfalar
  arasında gezinirken açık kalır, "Bitti" ile ya da sayfa yenilenince kapanır.
- R-3: Bir başlık sürüklenince alt başlıkları da onunla gider. Üç bırakma yeri vardır: bir başlığın **üstü** (onun
  önüne, aynı düzeye), **altı** (onun arkasına, aynı düzeye) ve **içi** (o başlığın alt başlığı olarak, sona).
  Birinci düzeydeki bir başlığın üstüne/altına bırakmak birinci düzeye taşır. Bırakılacak yer sürüklerken görünür
  biçimde gösterilir (çizgi ya da vurgu).
- R-4: Sürüklenen başlığın kendisi ve alt başlıkları bırakma hedefi olmaz (gösterge çıkmaz, bırakılınca bir şey
  olmaz). Sonucu bugünkü yerle aynı olan bırakma (üst başlığı ve kardeşleri arasındaki yeri değişmeyen; ör. kendi
  hemen altındaki çizgiye) kayıt yapmaz, bildirim çıkmaz. **Esc** ya da ağacın dışına bırakmak sürüklemeyi iptal eder;
  kayıt yapılmaz.
- R-5: Kapalı bir dalın üzerinde sürüklerken kısa süre (~0,7 sn) beklenirse dal açılır. Sürüklerken fare
  içindekiler panelinin üst ya da alt kenarına yaklaşınca panel kendiliğinden kayar (uzun derslerde görünmeyen yere
  taşıyabilmek için).
- R-6: Her bırakış hemen kaydedilir. Kayıt sürerken taşınan başlık beklemede görünür ve yeni bir sürükleme
  başlatılamaz; başarıda bildirim yeni numarayı gösterir ("Başlık taşındı; yeni numarası 2.3."); içindekiler,
  açık başlığın sayfası ve ders yolu yeni numaralarla güncellenir. Hata olursa (ör. sunucu reddi, bağlantı) gerekçeli
  mesaj gösterilir ve ağaç taşımadan önceki hâline döner.
- R-7: **Görünürlük (hassas alan):** taşıma, başlığın (ve dolayısıyla alt başlıklarının) etkin görünürlüğünü
  değiştirecekse kayıttan önce onay istenir ve sonuç açıkça yazılır: açıktan özele ("Bu başlık ve N alt başlığı
  giriş yapmamış okuyuculardan kalkacak.") ya da özelden açığa ("… herkese açılacak."). Vazgeçilirse kayıt yapılmaz,
  ağaç değişmez. Etkin görünürlük değişmiyorsa onay sorulmaz. Taşıma sonrası görünürlük, 0002'deki kurallarla
  okuma, içindekiler, madde ekranı ve aramada hemen geçerlidir.
- R-8: Klavye ve telefon için mevcut "Taşı" penceresi ve yukarı/aşağı düğmeleri olduğu gibi kalır (sürükle-bırakın
  erişilebilir eşdeğeri). Yönetici olmayan kullanıcılar "Sırala" düğmesini ve sürükleme tutamaçlarını görmez.

## User experience
- Flow: yönetici ders sayfasında (ya da bir başlık sayfasında) içindekilerin üstündeki "Sırala"ya basar →
  başlıkların yanında sürükleme tutamacı belirir → bir başlığı tutup başka bir başlığın üstüne/altına/içine
  sürükler (kapalı dal beklenince açılır) → bırakır → (görünürlük değişiyorsa onay) → bildirim, yeni numaralar →
  "Bitti".
- States:
  - Okuma modu (mod kapalı): bugünkü içindekiler; yöneticide "Sırala" düğmesi, diğerlerinde yok.
  - Sırala modu: tutamaçlar görünür; başlık bağlantıları gezinmeye devam eder.
  - Sürüklerken: sürüklenen başlık soluk; geçerli hedefte üst/alt çizgisi ya da "içine" vurgusu; geçersiz hedefte
    (kendisi ve altı) gösterge yok.
  - Görünürlük onayı: penceresi; "Taşı" ve "Vazgeç".
  - Kaydediliyor: taşınan başlık beklemede; yeni sürükleme yok.
  - Başarı: bildirim + yeni numaralar. Hata: gerekçeli mesaj + ağaç eski hâlinde.
  - Ders başlıksızken: "Sırala" yok (taşınacak bir şey yok); tek başlıkta "içine/üstüne" hedefi olmadığından
    yalnız mod açılır.
- Design reference: none — the flow above is the design. Görünüm mevcut içindekiler bileşenini izler.
- Accessibility: sürükleme tutamacının erişilebilir adı başlığı içerir ("Sürükle: 1.4 Ceza kanunlarının uygulama
  alanı"); "Sırala" düğmesi açık/kapalı durumunu bildirir (`aria-pressed`); klavye yolu R-8'deki düğmelerdir;
  sürükleme göstergeleri yalnız renge dayanmaz (çizgi/çerçeve).

## Constraints & out of scope
- Sensitive area: Yetki ve görünürlük (BR-1, BR-5) — taşıma etkin görünürlüğü değiştirebilir; yalnız yönetici taşır.
- Mevcut taşıma kuralları (spec 0002: kendi altına taşınamaz, adres değişmez, numara ağaçtan hesaplanır, yalnız
  yönetici) aynen geçerlidir; sürükle-bırak bunların yeni bir arayüzüdür.
- Performans: 300 başlıklı derste sürükleme akıcıdır (gözle görülür takılma yok); bir bırakışın kaydı ve
  içindekilerin güncellenmesi yerel ortamda 1 sn altında.
- Out of scope: derslerin sırasını sürükleyerek değiştirme; birden çok başlığı birlikte taşıma; telefonda/dokunmatikte
  sürükleme; geri al; ders sayfası "Bölümler" ve "Alt başlıklar" listelerinde sürükleme; başka derse taşıma.

## Acceptance criteria
- [ ] AC-1 — Yönetici içindekilerde "Sırala"yı açınca sürükleme tutamaçları görünür, "Bitti" ile kapanır; mod
  kapalıyken içindekiler bugünkü gibi çalışır. Okuyucu, standart ve okur kullanıcılar "Sırala"yı ve tutamaçları görmez.
- [ ] AC-2 — Bir başlık başka bir başlığın üstüne/altına bırakılınca o başlığın önüne/arkasına (aynı düzeye),
  içine bırakılınca alt başlığı olarak sona taşınır; birinci düzeyde üstüne/altına bırakma birinci düzeye taşır;
  alt başlıklar onunla gider.
- [ ] AC-3 — Taşımadan sonra taşınan başlığın, alt başlıklarının ve etkilenen kardeşlerin numaraları içindekilerde,
  başlık sayfasında, ders yolunda, madde ekranında ve aramada yeni değerleriyle görünür; adresleri değişmez;
  bildirim yeni numarayı gösterir.
- [ ] AC-4 — Başlığın kendisi ya da alt başlıklarından biri bırakma hedefi olmaz; sunucu taşımayı yine de reddederse
  gerekçeli mesaj gösterilir ve ağaç taşımadan önceki hâline döner; aynı yere bırakma (üst başlığı ve kardeşler
  arasındaki yeri değişmeyen) kayıt yapmaz ve bildirim çıkmaz; Esc ya da ağacın dışına bırakma sürüklemeyi iptal eder,
  kayıt yapılmaz.
- [ ] AC-5 — Etkin görünürlüğü değiştiren taşımada (açık → özel üst başlık ya da özel → açık) kayıttan önce onay
  istenir ve kaç başlığın etkileneceği yazılır; "Vazgeç" kayıt yapmaz; onaylanınca giriş yapmamış okuyucu için
  okuma, içindekiler, madde ekranı ve aramada yeni görünürlük hemen geçerlidir. Görünürlük değişmiyorsa onay sorulmaz.
- [ ] AC-6 — Taşıma yalnız yöneticiye açıktır: giriş yok → 401, `standard` ve `reader` → 403, ağaç değişmez
  (0002'nin taşıma kuralı sürükle-bırak yolunda da geçerli).
- [ ] AC-7 — Kapalı dalın üzerinde ~0,7 sn bekleyince dal açılır ve içindeki başlıklara bırakılabilir; sürüklerken
  panelin üst/alt kenarına yaklaşınca içindekiler kendiliğinden kayar; kayıt sürerken başlık beklemede görünür ve yeni
  sürükleme başlatılamaz.
- [ ] AC-8 — Klavyeyle ve telefonda (390 px) mevcut "Taşı" penceresi ve yukarı/aşağı düğmeleri çalışmaya devam eder;
  tutamaçların erişilebilir adı başlığı içerir, "Sırala" `aria-pressed` bildirir; dar ekranda ve telefon çekmecesinde
  "Sırala" görünmez; 390 px'te yatay taşma yok.

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
