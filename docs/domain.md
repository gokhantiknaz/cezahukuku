# Domain

> Ceza Hukukçusu'nun ortak dili. Bir terim burada yoksa ajan kendi anlamını uydurur — önce buraya ekleyin.

**Ürün:** hâkimler ve ceza hukukçuları için "hukukçu çalışma alanı": kanunlar, maddeler ve maddeye bağlı içerikler
(gerekçe, değişiklikler, açıklama, içtihat, norm denetimi, ilgili madde, 765 karşılığı) bir arada okunur ve aranır;
kullanıcı kendi vurgu ve notlarını tutar. Faz 1 (web) müşteride kabul testinde. Sonraki fazlar: Faz 2 Expo mobil,
Faz 3 sesli not yazıya dökme + OCR, Faz 4 mevzuat değişiklik takibi, Faz 5 kaynaklı yapay zekâ asistanı.

## Ubiquitous language

| Term | Meaning | Notes / not to be confused with |
|---|---|---|
| Kanun grubu → Kanun → Düğüm → Madde | Kütüphane ağacı. Düğüm türleri: kitap, kısım, bölüm, ayrım. | Kodda `LawGroup`, `Law`, `LawNode`, `Article`. |
| Sürüm | Maddenin bir yürürlük aralığındaki metni (`ArticleVersion`, `valid_from`/`valid_to`). | "Değişiklik gir" yeni sürüm açar; "Düzelt" sürümü yerinde değiştirir; "Önceki metni ekle" eski sürümü sonradan girer. |
| Fıkra / bent | Sürüm içindeki kalıcı kimlikli metin parçaları (`Paragraph`, `ParagraphItem`). | Sürümler arasında kimlik taşınır; vurgu ve değişiklik karşılaştırması bunlara bağlıdır. |
| Mülga | Yürürlükten kaldırılmış madde, fıkra ya da bent. | Silinmiş kayıt değildir; metinde "Mülga" görünür. |
| Suç tarihi (`?tarih=`) | Okuyucunun seçtiği gün; o gün yürürlükte olan sürüm gösterilir. | TCK 7 (lehe kanun) için. |
| Editoryal içerik | Yöneticinin girdiği gerekçe, açıklama, genel gerekçe, karar, madde bağlantısı, ders notu, haber, test sorusu. | Herkes görür (görünürlüğe göre). Kişisel içerikle karıştırılmaz. |
| Kişisel içerik | Kullanıcının kendi vurgu, not, sesli not ve belgeleri (Belgelerim). | Yalnız sahibi görür; `owner_id` zorunlu. |
| Karar | İçtihat (Yargıtay, BAM, AİHM…) ya da norm denetimi (AYM iptal/ret). | Bir kez girilir, çok maddeye bağlanır. |
| Madde bağlantısı | İlgili madde ya da eski kanun (765) karşılığı; kütüphanede yoksa serbest etiket. | Otomatik atıf (`ArticleCitation`) ayrıdır: metinden çıkarılır. |
| Ders notu | Kanundan bağımsız ders anlatımı; isteğe bağlı gruba, kanuna ya da maddeye bağlanır; belge eklenebilir. Derse bağlı değilse "düz ders notu"dur ("Diğer ders notları"). | Eski "madde notu olarak ders notu" kaldırıldı. |
| Ders | Ders kitabı gibi okunan numaralı başlık ağacının kökü (`LectureCourse`); adı, açıklaması, görünürlüğü, sırası vardır. | Adresi düz ders notlarıyla aynı alanda (`/ders-notlari/{adres}`), tekil. |
| Başlık | Bir dersin ağacındaki ders notu (`LectureNote` + `CourseId`, `ParentId`, `SortOrder`). Numarası ağaçtaki yerden hesaplanır ("1.5.1", birinci düzey "1. Bölüm"), elle girilmez; etkin görünürlüğü ders → üst başlıklar → kendisi zincirinde daralır (`LectureOutline`). | Numara okuyucudan bağımsızdır (ADR 0001). Alt başlığı olan başlık silinmez. |
| Görünürlük | `public`: herkes, SSR + SEO. `private`: yalnız giriş yapanlar. Üst öğeden miras alınır. | Rol değildir. |
| Roller | `super_admin` her şeyi yönetir · `standard` okur, arar, kişisel içerik tutar · `reader` yalnız okur. | |
| Ankraj | Vurgunun yeri: sürüm + fıkra + başlangıç/bitiş + alıntı (önek/sonek). | W3C Web Annotation modeli. |

## Business rules

- **BR-1:** `private` içerik anonim istekte 404 döner; varlığı aramada, listede ve site haritasında da sızmaz.
  Tek istisna: ders başlığı numaraları okuyucudan bağımsızdır; gizli başlığın bıraktığı numara boşluğu varlık
  sızıntısı sayılmaz (yalnız numara, ad/içerik/adres değil) — [ADR 0001](decisions/0001-lecture-numbers-independent-of-reader.md).
- **BR-2:** Kişisel içeriği yalnız sahibi görür ve değiştirir; yönetici de göremez.
- **BR-3:** Madde değişince eski sürüm silinmez, yeni sürümün yürürlük başlangıcından bir gün önce kapanır. Suç tarihi
  seçilirse o tarihte yürürlükte olan metin gösterilir.
- **BR-4:** Vurgu yeni sürüme alıntıyla yeniden bağlanır; bulunamazsa "eski sürüme ait" işaretlenir, kaybolmaz.
- **BR-5:** Editoryal içeriği yalnız `super_admin` yazar.
- **BR-6:** Kayıt kapalıdır; kullanıcıyı yönetici açar.
- **BR-7:** Yüklenen dosya, uzantısı ve içeriği (sihirli baytlar) eşleşirse kabul edilir; HTML ya da betik içerebilecek
  türler reddedilir.

## Key domain invariants

- BR-1, BR-2 ve BR-3 hiçbir özellik baskısıyla gevşetilmez.
- Kayıtlar soft delete ile silinir (`deleted_at`); fiziksel silme yoktur.
- Zengin metin sunucuda temizlenir; temizlenmemiş HTML veritabanına ve sayfaya girmez.

## Sensitive areas

Bu alanlara dokunan spec, Constraints bölümünde `Sensitive area: <ad>` yazar (lite modda da açıklama kapısı sorulur,
her düzeltme turu yeniden incelenir, her ölçüt kasıtlı bir bozmayla kırmızıya düşürülür).

1. **Yetki ve görünürlük** — private içerik sızması, rol denetimi (BR-1, BR-5).
2. **Kişisel veri (KVKK)** — notlar, vurgular, sesli notlar, belgeler, e-posta adresleri (BR-2).
3. **Kimlik doğrulama ve oturum** — giriş, çerez, hız sınırı (BR-6).
4. **Mevzuat metninin doğruluğu** — sürüm tarihleri, içe aktarma, suç tarihine göre metin (BR-3). Yanlış metin yanlış
   hükme yol açabilir.
5. **Dosya yükleme** — tür denetimi, boyut, depolama (BR-7).
