#!/usr/bin/env bash
# Günlük yedek: veritabanı (pg_dump) ve yüklenen dosyalar. 14 günden eskiler silinir.
# /etc/cron.d/cezahukuku-backup:  30 3 * * * root /opt/cezahukuku/src/deploy/backup.sh
# Yedekleri sunucu dışına da kopyalayın (ör. Hetzner Storage Box'a rsync); disk bozulursa sunucudaki yedek de gider.
set -euo pipefail

DIR=/var/backups/cezahukuku
STAMP=$(date +%Y-%m-%d)
mkdir -p "$DIR"
chmod 700 "$DIR"

sudo -u postgres pg_dump --format=custom cezahukuku > "$DIR/db-$STAMP.dump"
tar -czf "$DIR/files-$STAMP.tar.gz" -C /var/lib/cezahukuku files

find "$DIR" -type f -mtime +14 -delete
echo "Yedek alındı: $DIR (db-$STAMP.dump, files-$STAMP.tar.gz)"
