# Canlıya alma (tek Linux sunucu)

Kurulum tek bir VPS üzerinde çalışır. Dışarıya yalnız **Caddy** açılır: HTTPS sertifikasını kendisi alır ve
yeniler. Caddy istekleri Next.js'e (`127.0.0.1:3000`), Next.js de API'ye (`127.0.0.1:5080`) iletir. Veritabanı
PostgreSQL'dir. Docker gerekmez.

```
İnternet ──443──> Caddy ──> Next.js :3000 ──/api──> .NET API :5080 ──> PostgreSQL
                                                     └─> /var/lib/cezahukuku (PDF, ses, oturum anahtarları)
```

| Dosya | Sunucudaki yeri |
|---|---|
| `cezahukuku-api.service`, `cezahukuku-web.service` | `/etc/systemd/system/` |
| `Caddyfile` | `/etc/caddy/Caddyfile` |
| `api.env.example` | `/etc/cezahukuku/api.env` (şifreler burada; depoya girmez) |
| `deploy.sh` | kurulum ve her güncelleme: `sudo /opt/cezahukuku/src/deploy/deploy.sh` |
| `backup.sh` | günlük yedek (cron) |

## 1. Sunucu ve alan adı

- **Sunucu:** Ubuntu 24.04 LTS, 2 vCPU ve 4 GB RAM yeter. Örneğin Hetzner CX22 aylık yaklaşık 4–5 €.
  2 GB'lık sunucuda da çalışır, ama web derlemesi sırasında bellek sıkışabilir.
- **Alan adı:** DNS'te sunucunun IP adresine bir `A` kaydı açın (ör. `cezahukuku.com.tr`). `www` için de bir
  kayıt açın.
- `Caddyfile` içindeki `cezahukuku.example.com` yerine bu alan adını yazın.

## 2. Paketler

Komutları SSH ile bağlandığınız, `sudo` yetkili kullanıcıyla çalıştırın.

```bash
sudo apt update && sudo apt upgrade -y
sudo apt install -y git curl ufw

# .NET 10 SDK. Komut değişirse: https://learn.microsoft.com/dotnet/core/install/linux-ubuntu
sudo add-apt-repository -y ppa:dotnet/backports
sudo apt install -y dotnet-sdk-10.0

# Node.js 22
curl -fsSL https://deb.nodesource.com/setup_22.x | sudo -E bash -
sudo apt install -y nodejs

# PostgreSQL 17
sudo apt install -y postgresql-common
sudo /usr/share/postgresql-common/pgdg/apt.postgresql.org.sh -y
sudo apt install -y postgresql-17

# Caddy
sudo apt install -y debian-keyring debian-archive-keyring apt-transport-https
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | sudo tee /etc/apt/sources.list.d/caddy-stable.list
sudo apt update && sudo apt install -y caddy

# Güvenlik duvarı: yalnız SSH, HTTP ve HTTPS açık.
sudo ufw allow OpenSSH && sudo ufw allow 80 && sudo ufw allow 443 && sudo ufw --now enable
```

## 3. Uygulama kullanıcısı, klasörler, veritabanı

```bash
sudo useradd --system --create-home --home-dir /opt/cezahukuku --shell /usr/sbin/nologin cezahukuku
sudo install -d -o cezahukuku -g cezahukuku -m 700 /var/lib/cezahukuku /var/lib/cezahukuku/files /var/lib/cezahukuku/keys
sudo install -d -m 755 /etc/cezahukuku

# Veritabanı ve kullanıcısı: şifreyi kendiniz belirleyin (aşağıda api.env'e de yazılacak).
sudo -u postgres psql -c "CREATE USER cezahukuku WITH PASSWORD 'GUCLU-BIR-SIFRE';"
sudo -u postgres psql -c "CREATE DATABASE cezahukuku OWNER cezahukuku;"
```

PostgreSQL yalnız sunucunun içinden erişilebilir durumda kalır (varsayılan ayar), dışarıya açmayın.

## 4. Kod ve ayarlar

Depo özel (private) olduğu için sunucuya bir **deploy key** tanımlayın. Bu, yalnız okuma izni olan bir SSH
anahtarıdır:

```bash
sudo -u cezahukuku install -d -m 700 /opt/cezahukuku/.ssh
sudo -u cezahukuku ssh-keygen -t ed25519 -N "" -f /opt/cezahukuku/.ssh/id_ed25519
ssh-keyscan github.com | sudo -u cezahukuku tee -a /opt/cezahukuku/.ssh/known_hosts > /dev/null
sudo cat /opt/cezahukuku/.ssh/id_ed25519.pub
# GitHub → depo → Settings → Deploy keys → Add deploy key (yazma izni vermeyin), çıkan anahtarı yapıştırın.

sudo -u cezahukuku git clone git@github.com:gokhantiknaz/cezahukuku.git /opt/cezahukuku/src
```

Ayar dosyası:

```bash
sudo cp /opt/cezahukuku/src/deploy/api.env.example /etc/cezahukuku/api.env
sudo chmod 600 /etc/cezahukuku/api.env
sudo nano /etc/cezahukuku/api.env
```

Bu dosyada üç şeyi doldurun:
- Veritabanı şifresi (3. adımda belirlediğiniz).
- İlk yönetici e-postası.
- Geçici yönetici şifresi (en az 10 karakter).

## 5. Servisler ve ilk kurulum

```bash
sudo cp /opt/cezahukuku/src/deploy/cezahukuku-*.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable cezahukuku-api cezahukuku-web
sudo chmod +x /opt/cezahukuku/src/deploy/*.sh
sudo /opt/cezahukuku/src/deploy/deploy.sh
```

`deploy.sh` şunları yapar:
- API'yi derler.
- Web'i ayrı bir klasörde derler.
- Servisleri yeni sürümle yeniden başlatır.

İlk açılışta API veritabanı tablolarını kurar (`Database__MigrateOnStartup=true`) ve ilk yönetici hesabını açar.
Kontrol etmek için: `curl http://127.0.0.1:5080/api/health` → `{"status":"ok"}`.

## 6. Caddy (HTTPS)

```bash
sudo cp /opt/cezahukuku/src/deploy/Caddyfile /etc/caddy/Caddyfile
sudo nano /etc/caddy/Caddyfile        # alan adını yazın
sudo systemctl reload caddy
```

Birkaç saniye içinde `https://alan-adınız` açılır.

İlk girişten sonra:
- **Yönetim paneli → Kullanıcılar** ekranından yönetici şifresini değiştirin.
- `/etc/cezahukuku/api.env` içindeki `Seed__AdminPassword` satırını silin.

## 7. Yedek

```bash
echo "30 3 * * * root /opt/cezahukuku/src/deploy/backup.sh" | sudo tee /etc/cron.d/cezahukuku-backup
sudo /opt/cezahukuku/src/deploy/backup.sh     # bir kez deneyin
```

Yedekler `/var/backups/cezahukuku` klasöründe tutulur ve 14 gün saklanır. Ayrıca sunucu dışına da kopyalanmalıdır
(ör. Hetzner Storage Box'a `rsync` ile). Sunucu bozulursa, yalnız sunucudaki yedek de onunla birlikte gider.

Geri yükleme:

```bash
sudo -u postgres pg_restore --clean --if-exists -d cezahukuku /var/backups/cezahukuku/db-TARİH.dump
sudo tar -xzf /var/backups/cezahukuku/files-TARİH.tar.gz -C /var/lib/cezahukuku
```

## Güncelleme

Yeni kod `main` dalına gönderildikten sonra sunucuda şunu çalıştırın:

```bash
sudo /opt/cezahukuku/src/deploy/deploy.sh
```

Kesinti birkaç saniye sürer. Derleme hata verirse çalışan sürüme dokunulmaz. Önceki sürüm `/opt/cezahukuku/api.old`
ve `/opt/cezahukuku/web.old` klasörlerinde kalır.

## Sorun giderme

```bash
systemctl status cezahukuku-api cezahukuku-web caddy
journalctl -u cezahukuku-api -n 100 --no-pager      # API günlüğü
journalctl -u cezahukuku-web -n 100 --no-pager      # web günlüğü
sudo tail -f /var/log/caddy/cezahukuku.log          # gelen istekler
```

## Notlar

- **Oturumlar:** Oturum çerezleri `/var/lib/cezahukuku/keys` içindeki anahtarlarla şifrelenir. Bu klasör korunduğu
  sürece güncelleme ya da yeniden başlatma oturumları düşürmez. Klasör yalnız `cezahukuku` kullanıcısına açıktır
  (izin 700). API açılışında "anahtar şifresiz saklanıyor" uyarısı görülür; bu izinler altında beklenen bir
  durumdur.
- **Çerez güvenliği:** Canlıda çerez `Secure` işaretlidir, yani yalnız HTTPS üzerinden gönderilir. Siteyi HTTPS'siz
  bir IP adresinden açarsanız giriş yapılamaz.
- **İleride:**
  - Sesli notu yazıya dökme ve OCR, Faz 3'te eklenecek.
  - Trafik artarsa veritabanı ayrı bir sunucuya taşınabilir.
  - Dosyalar S3 uyumlu bir depoya alınabilir (`IFileStorage`).
