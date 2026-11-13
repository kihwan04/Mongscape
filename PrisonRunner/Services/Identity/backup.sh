#!/bin/sh
# Daily DB backup for the Docker server. Usage: ./backup.sh [backup-dir] (default /root/mongscape-backups).
# Backups hold face features and records: keep them private, outside Git/ZIP, and copy them off this server.
set -eu
cd "$(dirname "$0")"
dir="${1:-/root/mongscape-backups}"
keep_days="${MONGSCAPE_BACKUP_DAYS:-14}"
umask 077
mkdir -p "$dir"
file="$dir/mongscape-$(date +%Y%m%d-%H%M%S).dump"
# Write to a temporary name first so a failed dump never looks like a good backup.
docker compose exec -T postgres pg_dump -U postgres -d mongscape -Fc > "$file.partial"
mv "$file.partial" "$file"
find "$dir" -name 'mongscape-*.dump' -mtime +"$keep_days" -delete
echo "Backup written: $file"
