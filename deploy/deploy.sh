#!/usr/bin/env bash
# Sunucuda kurulum/güncelleme: kodu çeker, API'yi yayımlar, web'i ayrı klasörde derler, sonra eski sürümle
# değiştirip servisleri yeniden başlatır (kesinti birkaç saniye). Derleme hata verirse çalışan sürüme dokunulmaz.
# Kullanım: sudo /opt/cezahukuku/src/deploy/deploy.sh
set -euo pipefail

APP=/opt/cezahukuku
as_app() { sudo -u cezahukuku -H "$@"; }

cd "$APP/src"
as_app git pull --ff-only
echo "Sürüm: $(git log --oneline -1)"

echo "== API derleniyor"
rm -rf "$APP/api.new"
as_app dotnet publish backend/src/CezaHukuku.Api -c Release -o "$APP/api.new" -p:OpenApiGenerateDocuments=false -v q

echo "== Web derleniyor"
rm -rf "$APP/web.new"
as_app cp -a "$APP/src/web" "$APP/web.new"
(cd "$APP/web.new" && as_app npm ci --no-audit --no-fund && as_app env API_URL=http://127.0.0.1:5080 NEXT_TELEMETRY_DISABLED=1 npm run build)

echo "== Yeni sürüme geçiliyor"
systemctl stop cezahukuku-web cezahukuku-api
rm -rf "$APP/api.old" "$APP/web.old"
[ -d "$APP/api" ] && mv "$APP/api" "$APP/api.old"
[ -d "$APP/web" ] && mv "$APP/web" "$APP/web.old"
mv "$APP/api.new" "$APP/api"
mv "$APP/web.new" "$APP/web"
systemctl start cezahukuku-api

# Açılışta veritabanı şeması güncellenir; API hazır olmadan web başlatılmaz.
for i in $(seq 1 60); do
  if curl -fsS http://127.0.0.1:5080/api/health > /dev/null; then break; fi
  if [ "$i" -eq 60 ]; then
    echo "API 2 dakikada açılmadı: journalctl -u cezahukuku-api -n 100" >&2
    exit 1
  fi
  sleep 2
done
systemctl start cezahukuku-web
echo "Tamam. Sorun olursa önceki sürüm: $APP/api.old ve $APP/web.old"
