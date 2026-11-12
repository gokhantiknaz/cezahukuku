# Backlog — deferred findings

Findings a human triaged as **defer**: real, but fixed later by decision (`workflows/feature-development.md`,
step 7). Nothing lands here without that decision; noise is rejected in the triage record, not parked here.

- A criterion moved out at the ship gate (R-08, `→ moved to B-<n>`) lands here too, by the
  human's ship decision; the entry names the spec and criterion.
- One entry per finding, newest last. Delete the entry in the commit of the work that closes it.
- An entry that grows into new behavior becomes a spec (`specs/active/`), not a fix.

<!-- Format (labels stay English):
- **B-<n>** — <finding, one line> — Source: spec <NNNN>, review round <n> (<file:line>) — Severity: <…>
  — Recommendation: <the reviewer's action> — Deferred by: <name>, <YYYY-MM-DD>, <why>
  A decision deferred at bootstrap (its REPORT) uses Source: bootstrap <YYYY-MM-DD>.
-->

- **B-1** — .NET SDK sürümü sabit değil: yerel derleme 11.0.100-rc.3 ile yapılıyor, hedef `net10.0` (LTS); `backend/global.json` ile 10.0.403'e (rollForward: latestFeature) sabitlenmeli, CI ve canlı sunucu aynı SDK'yı kullanmalı — Source: bootstrap 2026-11-11 — Severity: low
  — Recommendation: `backend/global.json` ekle, 10.0.x ile tam derleme + testleri doğrula — Deferred by: Gokhan, 2026-11-11, bootstrap koda dokunmaz; ayrı küçük iş
- **B-2** — Mimari kurallar (docs/architecture.md, yasak bağımlılıklar 1–3) yalnız .csproj başvurularıyla tutuluyor; bir mimari test (ör. proje başvurularını okuyan birim testi) ile sabitlenmeli — Source: bootstrap 2026-11-11 — Severity: low
  — Recommendation: UnitTests'e Domain/Parsing/Infrastructure başvurularını denetleyen test ekle — Deferred by: Gokhan, 2026-11-11, bootstrap koda dokunmaz; ayrı küçük iş
- **B-3** — Web'de otomatik test yok; bileşen testleri (Vitest + Testing Library) ilk gerçek ihtiyaçta kurulmalı ve hızlı kontrole eklenmeli — Source: bootstrap 2026-11-11 — Severity: low
  — Recommendation: arayüz mantığı ağırlaşan ilk feature'ın planında kurulum adımı olarak ele al — Deferred by: Gokhan, 2026-11-11, şimdilik UI ölçütleri entegrasyon testi + VERIFY ekran görüntüleriyle kanıtlanıyor (seçenek A)
- **B-4** — Çıkıştan sonra kopyalanmış oturum çerezi süresi dolana kadar geçerli kalıyor — Source: bootstrap 2026-11-11 (2026-10-31 güvenlik testi bulgusu, 9ea95f2) — Severity: medium
  — Recommendation: çıkışta security stamp yenileme (tüm cihazlar ≤1 dk) ya da sunucu tarafı oturum tablosu (ITicketStore; cihaz bazında anında kapatma + "aktif oturumlar") — Deferred by: Gokhan, 2026-10-31, "çıkış şu an kalsın"
- **B-5** — Ortak pencere bileşeninin kapatma (X) düğmesinin ekran okuyucu adı İngilizce ("Close", `web/src/components/ui/dialog.tsx`) — Source: spec 0001, review round 2 (R2-2, `web/src/components/ui/dialog.tsx:78`) — Severity: low
  — Recommendation: sr-only metni "Kapat" yap (bütün pencereleri etkiler) — Deferred by: Gokhan, 2026-11-12, spec 0001'in kapsamı dışında, ortak bileşen
