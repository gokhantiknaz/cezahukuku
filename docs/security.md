# Security

> Her planda ve incelemede uyulacak temel kurallar (bootstrap 2026-11-11).

## Secrets
- Secrets never enter the repo, specs, prompts, or chat. `.env` is gitignored; provide `.env.example`.
- Agents never print secret values, even when debugging.
- Geliştirmede bağlantı dizesi API user-secrets'ta (`cezahukuku-api`, `ConnectionStrings:Default`); canlıda
  `/etc/cezahukuku/api.env` (şablonu `deploy/api.env.example`). psql şifreyi göstermeyen bir yardımcıyla çalıştırılır.
- Kullanıcının e-postası ve kişisel içerik dış servislere gönderilmez. Faz 5 (yapay zekâ asistanı) için ayrı karar
  gerekir; kişisel notlar varsayılan olarak gönderilmez.

## Input & output
- Veritabanına yalnız EF Core ya da parametreli SQL ile gidilir; arama sorgusu parametreli `to_tsquery`'ye çevrilir.
  Kullanıcı girdisiyle SQL metni birleştirilmez.
- Düz metin `InputText.Clean` / `CleanRequired`, zengin metin `RichText.Sanitize` (HtmlSanitizer) ile temizlenir.
- Dosya yükleme: uzantı ve içerik (sihirli baytlar) birlikte denetlenir, boyut ve istek sınırı vardır; depolama anahtarını
  sunucu üretir, kullanıcının dosya adı yol olarak kullanılmaz. HTML/SVG gibi betik taşıyabilen türler reddedilir.
- Hata yanıtında iç ayrıntı (yığın izi, SQL, dosya yolu) dönmez; ayrıntı Serilog günlüğüne yazılır.
- Güvenlik başlıkları açık; yönlendirmeler yalnız site içine (açık yönlendirme yok).

## AuthN / AuthZ
- Oturum: ASP.NET Core Identity cookie — HttpOnly, SameSite=Lax, canlıda Secure, kayan süreli. Kayıt kapalı (BR-6).
- Giriş ve araç uç noktalarında hız sınırı (`RateLimits`; istemci adresi güvenilen vekilin `X-Forwarded-For`'undan).
- **Varsayılan ret:** `/api/admin/**` grubu `Policies.AdminOnly`, `/api/me/**` grubu `Policies.PersonalLayer` ile
  korunur. Yeni uç nokta bu gruplara ya da açık bir `RequireAuthorization` çağrısına bağlanmadan eklenmez.
- Herkese açık okuma uç noktaları görünürlük filtresinden geçer; private kayıt 404 (BR-1). Kişisel veri sahip filtresiyle
  (BR-2).
- Çok parçalı yüklemelerde antiforgery kapalıdır; koruma SameSite=Lax çereze dayanır (bilinçli karar, kodda yorumlu).
- Bilinen, ertelenmiş açık: çıkıştan sonra kopyalanmış çerez süresi dolana kadar geçerli — `specs/backlog.md` B-4.

## Dependencies
- Yeni paket ancak bakımı süren, lisansı uygun (MIT, Apache-2.0, BSD) ve ihtiyacı plan/spec'te yazılı olursa eklenir;
  ajan eklemeden önce insana sorar.
- İncelemede `dotnet list backend/CezaHukuku.slnx package --vulnerable` ve `npm audit` (web) uyarılarına bakılır.

## Review lens
Security is a mandatory dimension of every independent review (see `prompts/review.md`), not a
separate afterthought phase.
