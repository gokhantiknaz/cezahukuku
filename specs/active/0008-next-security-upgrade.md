# Spec 0008 — Next.js güvenlik yükseltmesi (16.4.2 → 16.4.6) (mini)

- Status: Approved
- Mode: lite
- Plan: `specs/plans/0008-plan.md`
- Self-critique: done
- Source: plan 0007, risk 1 (Gokhan, 2026-11-20) — `npm audit --omit=dev`: next 16.0.0–16.4.4 aralığında 7 orta düzey
  bildirim; `next build` bildirim yüzünden aralıklı hata koduyla duruyor
- Design: yok — kullanıcı arayüzü değişmez.
- Supersedes: yok

## Intent
Web uygulamasının çatısı Next.js'in kullandığımız sürümünde (16.4.2) yayımlanmış güvenlik bildirimleri var ve derleme
komutu bu yüzden her iki çalıştırmadan birinde durup hata veriyor. Canlıya çıkmadan önce bildirimlerin kapandığı
16.4.6'ya geçilir. Okuyucunun ve yöneticinin gördüğü hiçbir şey değişmez.

## Changed behavior
- [ ] CB-1 — Web `next` ve `eslint-config-next` 16.4.6 sürümüyle (sabit sürüm) kurulur ve derlenir.
- [ ] CB-2 — `npm audit --omit=dev` çıktısında `next` için bildirim yoktur.
- [ ] CB-3 — `next build` art arda 3 çalıştırmada da hatasız biter; güvenlik bildirimi yüzünden durmaz.

## Preserved behavior
- [ ] PB-1 — Tür denetimi, lint, web birim testleri ve üretim derlemesi (`scripts/check` tam profil) yerelde ve GitHub
  Actions'ta yeşildir (S-2); mevcut testlerde değişiklik yoktur. Kilit dosyasındaki değişiklik yalnız `next`,
  `eslint-config-next` ve onların bağımlılıklarıyla sınırlıdır; ilgisiz paket sürümü değişmez (S-1).
- [ ] PB-2 — Üretim derlemesi yerel API ve veriyle açıldığında ana ekranlar açılır ve içerikleri gelir: ana sayfa,
  kanun (`/tck`), madde (`/tck/madde-233`), ders notları, giriş. Geliştirme sunucusu (`next dev`) da açılır ve sayfa
  döner (S-3).
- [ ] PB-3 — Son işlerin davranışı aynıdır: madde metnindeki parantez içi atıfa tıklayınca madde pencerede açılır
  (0005/0006) ve kayıtlı panel düzeniyle açılışta hydration uyarısı çıkmaz (bug 1).

## Out of scope
- Sensitive area: yok — kodda `use cache` / `unstable_cache` kullanılmıyor; kullanıcılar arası önbellek bildirimleri
  bizim kod yollarımıza denk gelmiyor. Görünürlük API tarafında, mevcut entegrasyon testleriyle korunuyor (G-3,
  Gokhan, 2026-11-20).
- Diğer paketlerin güncellenmesi; `shadcn` CLI'nin getirdiği bildirimler (`hono`, `ts-morph`, `minimatch`) — çalışma
  anında kullanılmıyor, B-18'e (G-2; B-18 bu işte backlog'a yazılır, S-4).
- Next'in ara ya da ana sürüm yükseltmesi (16.5, 17); uygulama kodu.
- Ekran görüntüsü: görünüm değişmediği için alınmaz; PB-2/PB-3 HTTP ve headless Edge gözlemiyle kanıtlanır (G-4).

## Definition of Done
- [ ] `scripts/check` green
- [ ] Independent review done; real findings fixed, noise rejected with written rationale, deferred ones in `specs/backlog.md`
- [ ] Criterion ↔ evidence table complete for CB-* **and** PB-* (UI: before/after screenshots, agent-captured, judged at SHIP)
- [ ] Spec moved to `specs/done/` (immutable there)
