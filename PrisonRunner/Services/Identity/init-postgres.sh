#!/bin/sh
(
set -eu
# Official entrypoint sources this file on first initialization only. Never echo passwords.
# Read the secret first: inside a command argument a failed $(cat ...) would not stop set -e,
# and an unreadable file would silently create the role with an empty (unusable) password.
secret=/run/secrets/db_app_password
if [ ! -r "$secret" ]; then
    echo "init-postgres: $secret is not readable by the postgres user. Keep secrets/* at mode 444 (folder 700), then recreate the DB volume." >&2
    exit 1
fi
app_password="$(cat "$secret")"
if [ "${#app_password}" -lt 32 ]; then
    echo "init-postgres: $secret is empty or too short. Run prepare_secrets.py, then recreate the DB volume." >&2
    exit 1
fi
psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --set=ON_ERROR_STOP=1 \
    --set=app_password="$app_password" <<'SQL'
CREATE ROLE mongscape LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD :'app_password';
ALTER DATABASE mongscape OWNER TO mongscape;
REVOKE ALL ON DATABASE mongscape FROM PUBLIC;
GRANT CONNECT, TEMPORARY ON DATABASE mongscape TO mongscape;
REVOKE ALL ON SCHEMA public FROM PUBLIC;
GRANT USAGE, CREATE ON SCHEMA public TO mongscape;
SQL
)
