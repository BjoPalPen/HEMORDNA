#!/usr/bin/env bash
#
# Daglig, krypterad säkerhetskopia av Hemordnas produktionsdatabas.
#
# Servern har BARA den publika nyckeln. Den kan alltså skriva en backup men aldrig läsa
# en - den som tar sig in på maskinen kommer åt den levande databasen, men inte åt
# historiken i backuperna. Den privata nyckeln finns hos Björn och ska aldrig hamna här.
#
# Återställning (på en maskin med den privata nyckeln):
#   gpg --decrypt hemordna-<tidpunkt>.sql.gz.gpg | gunzip | psql -U hemordna -d <måldatabas>
#
# Kör dagligen via cron, se scripts/db-backup/README.md.

set -euo pipefail

BACKUP_DIR="${HEMORDNA_BACKUP_DIR:-/home/deploy/backups}"
RECIPIENT="${HEMORDNA_BACKUP_RECIPIENT:-backup@hemordna.se}"
CONTAINER="${HEMORDNA_POSTGRES_CONTAINER:-hemordna-postgres-1}"
DB_NAME="${POSTGRES_DB:-hemordna}"
DB_USER="${POSTGRES_USER:-hemordna}"
RETENTION_DAYS="${HEMORDNA_BACKUP_RETENTION_DAYS:-30}"

# En tom eller trunkerad dump kan annars se ut som en giltig backup. Produktionsdatabasen
# är flera megabyte; allt under det här är ett fel oavsett vad exitkoderna påstår.
MIN_BYTES="${HEMORDNA_BACKUP_MIN_BYTES:-20000}"

mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"

stamp="$(date -u +%Y%m%dT%H%M%SZ)"
target="$BACKUP_DIR/hemordna-$stamp.sql.gz.gpg"
partial="$target.part"

# Städa bort en halvskriven fil om något i kedjan fallerar - annars ligger den kvar och ser
# ut som en backup vid nästa titt.
cleanup() { rm -f "$partial"; }
trap cleanup EXIT

# Hela kedjan är en pipe: ingenting okrypterat rör disken. `pipefail` gör att ett fel var
# som helst i pipen fäller hela skriptet innan filen hinner få sitt riktiga namn.
docker exec "$CONTAINER" pg_dump -U "$DB_USER" -d "$DB_NAME" \
  | gzip -9 \
  | gpg --batch --yes --trust-model always \
        --encrypt --recipient "$RECIPIENT" \
        --output "$partial"

size="$(stat -c %s "$partial")"
if [ "$size" -lt "$MIN_BYTES" ]; then
  echo "backup-db: avbryter, filen är bara $size byte (minst $MIN_BYTES förväntas)" >&2
  exit 1
fi

# Byt namn först när filen är komplett och rimlig. Ett avbrott före det här lämnar ingen
# fil som kan förväxlas med en färdig backup.
mv "$partial" "$target"
chmod 600 "$target"
trap - EXIT

find "$BACKUP_DIR" -maxdepth 1 -name 'hemordna-*.sql.gz.gpg' -mtime "+$RETENTION_DAYS" -delete

echo "backup-db: $(basename "$target") ($size byte)"
