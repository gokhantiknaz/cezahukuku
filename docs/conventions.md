# Conventions

> Yalnız gerçek kurallar (bootstrap 2026-11-11): her kural ya araçla zorunlu tutulur ya da incelemede denetlenir.

## Language
<!-- Set at bootstrap (step 0) and mirrored in AGENTS.md. Changing it later is allowed — edit both
     places; existing documents are not translated retroactively. -->
- Chat language (interviews, sessions): Türkçe
- Document language (docs/, specs, plans, ADRs, review and verify reports): Türkçe
- Always English (protocol, not prose): ANEW core files, template headings and field labels,
  `Status:` values (Draft / Approved / In progress / Shipped), `Approved by / on:`, `Source:`.
- Code identifiers, branch names, commit messages: tanımlayıcılar ve dal adları İngilizce; kod yorumları, arayüz metni ve
  commit mesajları Türkçe (Conventional Commits öneki İngilizce — `docs/git.md`).
- Agent messages (handoff summaries, refusals, `Next:` lines): chat language; command names, file
  paths and protocol fields verbatim.

## Language & framework versions
- Backend: .NET 10 (`net10.0`, LTS), C# `Nullable` ve `ImplicitUsings` açık; EF Core 10 + Npgsql; PostgreSQL 17.
  SDK sabitlenmesi backlog'da (B-1).
- Web: Next.js 16.4 (App Router, `cacheComponents`), React 19.3, TypeScript 5 (strict), Tailwind 4, shadcn/ui,
  TanStack Query, orval. Node 22.

## Naming
- C#: tipler ve üyeler İngilizce, PascalCase; uç nokta sınıfları `<Alan>Endpoints`, istek/yanıt kayıtları
  `Save…Request`, `…Dto`, `…Detail`, `…Summary`.
- Veritabanı: snake_case (EFCore.NamingConventions); migration adı PascalCase (`LectureNoteFiles`).
- Web: dosya adları kebab-case (`article-quick-add.tsx`), bileşenler PascalCase; URL'ler Türkçe ve kebab-case
  (`/ders-notlari`, `/tck/madde-81`).
- Testler: `<Konu>Tests` sınıfı, `Davranis_Kosul_Sonuc` biçiminde İngilizce metot adı.

## Error handling
- Doğrulama hatası → 400, alan bazında Türkçe mesaj (`ValidationErrors`, `RequestValidation`).
- Bulunamayan ya da görünmeyen kayıt → 404 (private kayıt da 404, 403 değil — BR-1).
- Giriş yok → 401; rol yetmiyor → 403.
- İç hata ayrıntısı kullanıcıya dönmez; Serilog ile günlüğe.
- Web: `apiFetch` 2xx dışında `ApiError` fırlatır; `ApiError.messages` toast ya da form alanı hatası olarak gösterilir.

## Data rules
- Kimlikler UUID v7 (`Guid.CreateVersion7`).
- Zaman damgaları `DateTimeOffset`, UTC (`created_at`, `updated_at` — `TimestampInterceptor`); gün bilgisi `DateOnly`.
- Silme her zaman soft delete (`deleted_at` + sorgu filtresi).
- Düz metin `InputText.Clean`, zengin metin `RichText.Sanitize` ile temizlenir.
- Türkçe büyük/küçük harf: sunucuda `tr-TR` kültürü, web'de `toLocaleLowerCase("tr")` (İ/ı).

## Known traps
- Next 16 `cacheComponents` ziyaret edilen sayfayı gizli tutar ve durumunu korur — formlar `key` ile ya da kayıttan
  sonra sıfırlanır, yoksa eski veriyle açılır — from ders notu formu (137c5ea).
- PowerShell `Get-Content`/`Set-Content` Türkçe karakterleri bozar (cp1254) — dosyalar bash ya da düzenleme aracıyla
  yazılır — from geliştirme deneyimi.
- API projesini derlemek `backend/openapi/cezahukuku-api.json`'u yeniden üretir — DTO değişince ardından
  `npm run gen:api`; düz derleme/testte `-p:OpenApiGenerateDocuments=false` — from geliştirme deneyimi.
- Çalışan API derleme çıktısını kilitler — check `artifacts/check`'e derler; `dotnet test` öncesi aynı çıktıyı
  kullanan süreç durdurulur — from geliştirme deneyimi.
- `ZipArchive`/`PdfDocument` verilen akışı kapatır — ortak buffer'ı salt okunur ayrı bir `MemoryStream` ile ver —
  from belge ekleme (137c5ea).
- tsvector 1 MB'ı aşamaz — büyük gövdeyi indekslemeden önce kısalt (dosya metni 200.000 karakter) — from belge ekleme.
- Bağlı istemci varken PostgreSQL `TEMPLATE` kopyası başarısız olur — `pg_dump | psql` kullan — from e2e hazırlığı.

## Enforced by tooling
- C# derleyicisi: nullable uyarıları, xUnit analizörleri (`dotnet build`, check `build`).
- TypeScript strict (`tsc --noEmit`, check `typecheck`), ESLint + `eslint-config-next` (check `lint`).
- Next derlemesi (check `web build`): sunucu/istemci bileşen sınırları, `cacheComponents` kuralları.
