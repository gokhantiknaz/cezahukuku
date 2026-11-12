# Spec 0001 — Madde sekmelerinde kayıtları düzenleme ve kaldırma

- Status: Shipped
- Mode: lite
- Plan: `specs/plans/0001-plan.md`
- Self-critique: done

## Intent
Yönetici, maddenin okuma ekranındaki Gerekçe, Açıklama, İçtihatlar ve Norm Denetimi sekmelerinde duran kayıtları
yerinde düzeltmek ya da kaldırmak istiyor; bugün bunun için yönetim panelindeki madde düzenleme ekranına gitmesi
gerekiyor. Başarı: kaydın yanındaki düğmeyle düzenleme penceresi açılır, kaydedilir ya da onaylanarak kaldırılır ve
değişiklik aynı sekmede hemen görünür. Okuyucu ve üyeler bu düğmeleri görmez; yeni bir yetki kuralı eklenmez, "editoryal
içeriği yalnız yönetici yazar" (BR-5) aynen geçerlidir. Bilinçli olarak yapılmayanlar: ilgili madde / 765 karşılığı
bağlantıları, Açıklama sekmesindeki ders notu bağlantıları ve kararı tümden silme (o, karar yönetim sayfasında kalır).

## Requirements
- R-1: Gerekçe ve Açıklama sekmelerindeki her editoryal notun yanında, yalnız yöneticiye, "Düzenle" ve "Kaldır" düğmeleri
  görünür.
- R-2: İçtihatlar ve Norm Denetimi sekmelerindeki her kararın yanında, yalnız yöneticiye, "Düzenle" ve "Bu maddeden kaldır"
  düğmeleri görünür.
- R-3: "Düzenle" bir pencere açar; pencere, ekleme penceresiyle aynı alanları kaydın mevcut değerleriyle dolu gösterir
  (not: başlık, görünürlük, metin; karar: tür, mahkeme, daire, esas/karar no ve tarihi, sonuç, Resmî Gazete, özet, metin,
  kaynak, görünürlük). Görünürlük alanı kaydın kendi ayarını gösterir; sekmedeki "Özel" rozeti etkin (miras dahil)
  görünürlüğü göstermeye devam eder. Kaydedince pencere kapanır, değişiklik açık sekmede görünür, açık sekme değişmez.
- R-4: Başka maddelere de bağlı bir karar düzenlenirken pencere, bağlı olduğu maddeleri listeler ve değişikliğin hepsinde
  görüneceğini söyler.
- R-5: "Kaldır" (not) ve "Bu maddeden kaldır" (karar) önce onay ister. Not okuyuculardan kaldırılır (kayıt saklanır).
  Karar yalnız bu maddeyle bağlantısını kaybeder; karar ve diğer maddelerle bağlantıları kalır.
- R-6: Vazgeçilen ya da kapatılan pencere hiçbir şeyi değiştirmez.
- R-7: Bir işlem başarısız olursa (kayıt bu arada silinmiş, doğrulama hatası, yetki) sunucunun mesajı gösterilir; düzenleme
  penceresi açık kalır ve girilenler kaybolmaz; sekme güncel hâliyle yenilenir.
- R-8: Düzenleme sonucunda değişen görünürlük hemen geçerli olur: "üyelere özel" yapılan kayıt giriş yapmamış okuyucuya
  (okuma ekranı ve arama) görünmez (BR-1).
- R-9: Düğmeler suç tarihi seçimiyle (`?tarih=`) değişmez; notlar ve kararlar madde sürümüne bağlı değildir.

## User experience
- Flow: Yönetici madde sayfasında bir sekmeyi açar → kaydın sağ üstündeki kalem ya da çöp kutusu düğmesine basar →
  (Düzenle) dolu pencere → değiştirir → Kaydet → pencere kapanır, sekmede güncel hâl · (Kaldır) onay penceresi →
  Kaldır → kayıt sekmeden kalkar.
- States:
  - Okuyucu / üye / giriş yok: düğme yok.
  - Yönetici, sekme boş: düğme yok (yalnız mevcut "… ekle" düğmesi).
  - Yönetici, kayıt var: her kayıtta iki simge düğmesi.
  - Düzenleme penceresi: karar bilgileri gelirken "Yükleniyor…"; kaydederken Kaydet düğmesi pasif; hata → mesaj, pencere
    açık; başarı → pencere kapanır + "… güncellendi." bildirimi.
  - Onay penceresi: başlık ve açıklama (not: "okuyuculardan kaldırılır, kayıt veritabanında saklanır"; karar: "yalnız bu
    maddeden kaldırılır, karar silinmez"); Vazgeç / Kaldır; başarı → "… kaldırıldı." bildirimi.
- Design reference: none — the flow above is the design. Düğmeler mevcut panel listesindeki simge düğmeleriyle (kalem,
  çöp kutusu) ve 0001 öncesi eklenen "… ekle" penceresiyle aynı görünümde.
- Accessibility: düğmeler klavyeyle (Tab) ulaşılır; erişilebilir adları kaydı içerir ("Gerekçeyi düzenle: <başlık ya da
  sıra>", "Kararı bu maddeden kaldır: <künye>"); pencereler odak tuzaklı, Esc ile kapanır; yazdırmada düğmeler gizli;
  390 px genişlikte sayfa ve pencere yatay taşmaz.

## Constraints & out of scope
- Sensitive area: Yetki ve görünürlük (BR-1, BR-5).
- Sunucuda yeni yetki kuralı ya da yeni veri alanı yok; mevcut yönetici işlemleri kullanılır.
- Out of scope: ilgili madde / 765 karşılığı bağlantılarını düzenleme-kaldırma; ders notu bağlantıları; kararı tümden
  silme (karar yönetim sayfasında kalır); kaldırılan notu geri getirme; Değişiklikler sekmesi; eşzamanlı düzenleme
  denetimi (iki yönetici aynı kaydı düzenlerse son kaydeden kazanır).

## Acceptance criteria
- [x] AC-1 — Yönetici Gerekçe ve Açıklama sekmelerinde bir notu pencereden düzenler: pencere mevcut başlık, görünürlük ve
  metinle dolu açılır; kaydedince pencere kapanır, yeni metin aynı sekmede görünür ve sekme değişmez.
- [x] AC-2 — Yönetici bir notu onay penceresiyle kaldırır: onaylayınca not sekmeden ve giriş yapmamış okuyucunun
  sayfasından kalkar (sekmede başka not kalmazsa boş durum metni görünür); onay penceresinde Vazgeç'e basılırsa not
  yerinde kalır.
- [x] AC-3 — Yönetici İçtihatlar ve Norm Denetimi sekmelerinde bir kararı pencereden düzenler: pencere mevcut değerlerle
  dolu açılır; kaydedince künye/özet sekmede ve aramada güncel hâliyle görünür.
- [x] AC-4 — Başka maddelere de bağlı bir karar düzenlenirken pencere bağlı maddeleri listeler.
- [x] AC-5 — "Bu maddeden kaldır" onaylanınca karar bu maddenin sekmesinden kalkar; aynı karar bağlı olduğu diğer maddede
  ve karar yönetim listesinde durmaya devam eder; sekmede başka karar kalmazsa boş durum metni görünür.
- [x] AC-6 — Düzenleme penceresi Vazgeç ya da Esc ile kapatılırsa kayıt değişmez.
- [x] AC-7 — Yönetici bir notu ya da kararı "üyelere özel" yapınca giriş yapmamış okuyucu onu okuma ekranında ve aramada
  artık görmez; giriş yapmış üye görür.
- [x] AC-8 — Giriş yapmamış okuyucu, `standard` ve `reader` kullanıcılar düğmeleri görmez; yönetici olmayanın düzenleme ve
  kaldırma istekleri sunucuda reddedilir (giriş yok → 401, rol yetmiyor → 403) ve kayıt değişmez.
- [x] AC-9 — Kayıt bu arada silinmişse ya da doğrulama hatası varsa sunucunun mesajı gösterilir; düzenleme penceresi açık
  kalır, girilenler kaybolmaz; kayıt silinmişse sekme güncel hâliyle yenilenir.
- [x] AC-10 — Düğmeler klavyeyle ulaşılır ve erişilebilir adları kaydı içerir; yazdırmada gizlidir; 390 px genişlikte sayfa
  ve pencere yatay taşmaz.

## Definition of Done
- [x] Every acceptance criterion mapped to proof (test or reproducible observation)
- [x] `scripts/check` green
- [x] Independent review done; real findings fixed, noise rejected with written rationale, deferred ones in `specs/backlog.md`
- [x] Docs / ADRs updated if behavior or architecture changed
- [x] Spec moved to `specs/done/` (it becomes immutable there)

## Scorecard (fill at ship — honest numbers make the process improvable)
| Metric | Value |
|---|---|
| Spec revisions | 0 onay sonrası (A-1 yalnız test adı/yorum) |
| Fix rounds | 3 |
| Review findings: real / noise / deferred | 10 / 1 / 1 (B-5) |
| Regressions introduced | 0 |
| Bugs escaped to production | 0 (henüz canlıya alınmadı) |
