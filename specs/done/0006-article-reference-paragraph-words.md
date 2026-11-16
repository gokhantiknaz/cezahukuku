# Spec 0006 — Parantez içi atıflarda "fıkra" ve "bent" yazımı (mini)

- Status: Shipped
- Mode: lite
- Plan: `specs/plans/0006-plan.md`
- Self-critique: done
- Source: kullanıcı talebi 2026-11-16 (ekran görüntüsü: TCK 233/4, "(madde 87, fıkra 2 ve 4)" işaretsiz)
- Design: yok — görünüm 0005'teki işaretin aynısı.
- Supersedes: 0005/AC-2

## Intent
Kanun metinlerinde parantez içi atıf, fıkra ve bent de kelimeyle yazılarak geçiyor: "(madde 87, fıkra 2 ve 4)" (TCK
233/4). Spec 0005'te bu biçim tanınmıyor; parantezin içinde "fıkra" kelimesi olduğu için atıf hiç işaretlenmiyor.
Bu değişiklikle böyle yazılmış atıfta da madde numarası işaretlenir ve tıklanınca madde pencerede açılır. Fıkra ve
bent numaraları madde sanılıp başka maddeye bağlanmaz.

## Changed behavior
- [x] CB-1 — Madde numarasının ardından virgülle (ya da virgülsüz) gelen fıkra yazımı tanınır: "(madde 87, fıkra 2)",
  "(madde 87, fıkra 2 ve 4)", "(madde 87, fıkra 2, 3 ve 4)", "(madde 87 fıkra 2)", "(md. 87, fık. 2)". Yalnız "87"
  işaretlenir ve TCK 87'yi (atıfın kanunundaki 87. maddeyi) açar.
- [x] CB-2 — Fıkradan sonra ya da tek başına bent yazımı tanınır: "(madde 53, fıkra 1, bent c)", "(madde 53, bent c)",
  "(madde 53, fıkra 1, bendi c ve d)". Yalnız "53" işaretlenir.
- [x] CB-3 — Fıkra ve bent numaraları madde olarak işaretlenmez: "(madde 87, fıkra 2 ve 4)" içinde "2" ve "4" düz metin
  kalır, 2. ve 4. maddelere bağlantı kurulmaz.
- [x] CB-4 — Birden çok madde numarasından sonra gelen fıkra yazımı son maddeye aittir: "(madde 81, 82, fıkra 1)" içinde
  "81" ve "82" işaretlenir, "1" işaretlenmez.
- [x] CB-5 — Madde ekranında (TCK 233) ve ders notunda bu biçimler 0005'teki gibi işaretli görünür ve pencereyi açar.

## Preserved behavior
- [x] PB-1 — 0005'te tanınan biçimler aynen tanınır ve aynı parçayı işaretler: "(madde 21)", "(madde 21, 22)",
  "(madde 21 ve 22)", "(maddeler 21, 22)", "(m. 21)", "(md.21)", "(madde 21/2)", "(madde 53/1-c)".
- [x] PB-2 — 0005'te tanınmayan biçimler tanınmamaya devam eder: "madde 21'e göre" (parantezsiz), "(madde 21-23)",
  "(madde 21 ila 23)", "(TCK m. 21)", "(geçici madde 1)", "(madde)", "(maddenin 21 inci fıkrası)". Parantezin içinde
  tanınmayan başka bir parça varsa ("(madde 87, fıkra 2 hariç)") parantez yine hiç işaretlenmez.
- [x] PB-3 — Kanun seçimi, görünürlük (okuyucunun göremediği madde işaretlenmez, BR-1), suç tarihi, pencere ve
  yönetim ekranlarındaki davranış 0005'teki gibidir.

## Out of scope
- Sensitive area: yok — yalnız tanıma kuralı değişir; görünürlük ve sürüm/tarih mantığı 0005'teki gibidir (PB-3).
  Fıkra numarasının madde sanılması riski CB-3 ile ayrıca sınanır (G-1, Gokhan, 2026-11-16).
- "fıkrası", "fıkrasının" gibi ekli yazımlar (G-4).
- Fıkra ya da bente göre pencerede o fıkranın vurgulanması ya da kaydırılması (pencere maddenin tamamını açar).
- Sözle yazılmış sıra sayıları ("birinci fıkra", "ikinci fıkrası"), "f." ve "b." kısaltmaları, noktalı virgülle
  ayrılmış birden çok atıf ("(madde 87, fıkra 2; madde 89)").
- Parantezin tamamının boyanması: işaret 0005'teki gibi yalnız madde numarasındadır (G-2). Görünüm değişikliği
  istenirse ayrı bir değişiklik talebidir.
- Gerekçe, açıklama, içtihat sekmeleri (B-13).

## Definition of Done
- [x] `scripts/check` green
- [x] Independent review done; real findings fixed, noise rejected with written rationale, deferred ones in `specs/backlog.md`
- [x] Criterion ↔ evidence table complete for CB-* **and** PB-* (UI: before/after screenshots, agent-captured, judged at SHIP)
- [x] Spec moved to `specs/done/` (immutable there)
