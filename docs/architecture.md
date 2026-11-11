# Architecture

> Kararlaştırılmış mimari (bootstrap 2026-11-11). Ajanlar plan yapmadan önce bunu okur.

## System overview

İki ayrı kurulup çalışan uygulama: **.NET 10 API** (ASP.NET Core Minimal API) ve **Next.js 16 web**. Web `/api/*`
isteklerini Next `rewrites` ile API'ye iletir; böylece aynı origin, Identity cookie'si sunucu tarafı çizimde de geçer,
CORS yoktur. Tek veritabanı PostgreSQL 17; arama da aynı veritabanında (`search_docs`, Türkçe FTS). Mikroservis ya da
ayrı arama motoru yok: trafik düşük, darboğaz veritabanı. Canlı: tek Linux VPS (Caddy → Next.js → API → PostgreSQL),
ayrıntı `deploy/README.md`.

## Modules / components and ownership

| Module | Single responsibility | Owns |
|---|---|---|
| `backend/src/CezaHukuku.Domain` | Varlıklar ve alan kuralları (görünürlük mirası, sürüm, ankraj) | Alan modeli |
| `backend/src/CezaHukuku.Parsing` | Mevzuat metni, atıf, künye ve arama sorgusu ayrıştırma; Türkçe normalize | Saf fonksiyonlar, durum yok |
| `backend/src/CezaHukuku.Infrastructure` | EF Core + Npgsql, migration'lar, arama indeksi, dosya depolama (`IFileStorage`), belge metni çıkarma, arka plan işleyici | Veritabanı şeması ve dosyalar |
| `backend/src/CezaHukuku.Api` | Uç noktalar (Library, Content, Personal, Admin, Search, Tools, Auth), yetki politikaları, OpenAPI | HTTP sözleşmesi |
| `web/` | Okuma ekranları (SSR + SEO), yönetim paneli (`/panel`), kişisel alan | Arayüz durumu |
| `deploy/` | Tek sunucu kurulumu (Caddy, systemd, yedek) | Canlı yapılandırma şablonları |

## Communication rules

- Web, API'ye yalnız orval ile üretilmiş istemci (`web/src/api/generated`) ya da `apiFetch` / `server-api` üzerinden
  erişir. C# DTO değişince API derlenir (OpenAPI belgesi yeniden üretilir), ardından `npm run gen:api`; üretilmiş
  dosyalar elle düzenlenmez.
- Arama indeksi kayıt sırasında (`SearchIndexChanges`, `AppDbContext` kaydından sonra) güncellenir; ayrı senkron işi yok.
- Uzun işler (PDF metni, yazıya dökme) veritabanı durumlu arka plan işleyiciyle yürür; HTTP isteği beklemez.

## Forbidden dependencies (make them testable)

1. `Domain` hiçbir projeye başvurmaz.
2. `Parsing` hiçbir projeye başvurmaz; EF Core ve HTTP kullanmaz.
3. `Infrastructure`, `Api`'ye başvurmaz.
4. Web veritabanına ya da dosya deposuna doğrudan erişmez; yalnız `/api`.
5. Görünürlük denetimi tek yerde: `PrincipalExtensions.CanSeePrivate` ve sorgu filtreleri. Okuma, arama, listeleme aynı
   kuralı kullanır.
6. Kişisel veriye her sorgu sahip filtresiyle (`OwnerId`) erişir.

1–3 bugün `.csproj` başvurularıyla tutuluyor; bir mimari testle sabitlenmesi backlog'da (B-2).

## Deliberately out of scope

Çevrimdışı mobil · açık kayıt · Meilisearch/Elasticsearch · çok kiracılı yapı · tamamen Cloudflare üzerinde barındırma
(Cloudflare önde + VPS seçeneği notlarda bekliyor).
