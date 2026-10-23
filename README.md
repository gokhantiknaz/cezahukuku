# Ceza Hukukçusu

Ceza hukuku mevzuatı, gerekçeler, içtihatlar ve kişisel notlar için çalışma platformu.

| Klasör | İçerik |
|---|---|
| `backend/` | .NET 10 Web API (`CezaHukuku.Api`), Domain, Infrastructure (EF Core + PostgreSQL), Parsing, testler |
| `backend/openapi/` | Derlemede üretilen OpenAPI belgesi (web istemcisinin kaynağı) |
| `web/` | Next.js uygulaması (okuma ekranı + `/panel` yönetimi) |

## Gereksinimler

- .NET 10 SDK (net10.0 hedeflenir)
- Node.js 22+
- PostgreSQL 17 (yerel kurulum ya da bulut, ör. Neon)

## İlk kurulum

```bash
dotnet tool restore                     # dotnet-ef
cd web && npm install && cd ..
```

Geliştirme bağlantısı `backend/src/CezaHukuku.Api/appsettings.Development.json` içindedir
(`Host=localhost;Database=cezahukuku;Username=postgres;Password=postgres`). Farklıysa
kullanıcı sırlarıyla ezin (repoya girmez; entegrasyon testleri de buradan okur):

```bash
dotnet user-secrets --project backend/src/CezaHukuku.Api set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=cezahukuku;Username=postgres;Password=..."
```

Geliştirmede boş veritabanına küçük bir örnek kütüphane (gruplar, kanunlar, TCK 81–85) eklenir
(`Seed:DemoLibrary`).

## Çalıştırma

```bash
dotnet run --project backend/src/CezaHukuku.Api   # http://localhost:5080 (API dokümanı: /scalar)
cd web && npm run dev                             # http://localhost:3000
```

Development ortamında API açılışta migration'ları uygular ve rolleri oluşturur. Hiç yönetici yoksa
`Seed:AdminEmail` / `Seed:AdminPassword` ile ilk `super_admin` açılır
(geliştirmede: `admin@cezahukuku.local` / `Gelistirme123`). Dışarıdan kayıt kapalıdır
(`Auth:AllowRegistration=false`); kullanıcılar `/panel/kullanicilar` sayfasından açılır.

## Sık kullanılan komutlar

```bash
dotnet test backend/CezaHukuku.slnx               # birim + entegrasyon testleri
dotnet test backend/tests/CezaHukuku.UnitTests    # yalnız birim testleri (veritabanı gerekmez)
dotnet ef migrations add <Ad> --project backend/src/CezaHukuku.Infrastructure \
  --startup-project backend/src/CezaHukuku.Api --output-dir Persistence/Migrations
cd web && npm run gen:api                         # API değişince: backend build → tipli istemciyi yeniden üret
cd web && npm run lint && npm run build
```

Entegrasyon testleri gerçek API'yi aynı sunucudaki ayrı `cezahukuku_integration` veritabanına karşı
çalıştırır; bu veritabanı her test oturumunda silinip yeniden kurulur. Elle deneme yaptığınız geliştirme
veritabanından (user-secrets'taki `Database=`) bilerek ayrıdır.
Başka bir sunucu için `CEZAHUKUKU_TEST_CONNECTION` ortam değişkenini ayarlayın.
