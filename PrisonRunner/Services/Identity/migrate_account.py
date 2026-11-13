"""Explicit, atomic upgrade of an existing PostgreSQL identity DB to the current schema.
Every step is idempotent and all steps share one transaction. Back up the DB and stop the API first."""
import argparse
import json
import os
import unicodedata
from storage import NAME_MAX,SCHEMA_VERSION,ConfigError,Store,name_key

def drop_profile_avatar(store):
    """Face photos are no longer stored; remove the old column and every photo in it."""
    if 'avatar' not in store.columns('profiles'):return False
    store.db.execute('ALTER TABLE profiles DROP COLUMN avatar')
    return True

# ADD COLUMN NOT NULL needs a default for existing rows.
PROFILE_COLUMNS=(('name_key',"TEXT NOT NULL DEFAULT ''"),('weight','DOUBLE PRECISION NOT NULL DEFAULT 65'),
    ('style','INTEGER NOT NULL DEFAULT 0'),('goal','DOUBLE PRECISION NOT NULL DEFAULT 50'),
    ('coins','INTEGER NOT NULL DEFAULT 0 CHECK (coins >= 0)'),('unlocked',"JSONB NOT NULL DEFAULT '[]'"),
    ('tutorial_done','BOOLEAN NOT NULL DEFAULT FALSE'))
RUN_COLUMNS=(('mode','INTEGER'),('coins','INTEGER NOT NULL DEFAULT 0'),
    ('score_version','INTEGER NOT NULL DEFAULT 1'),('play_seconds','DOUBLE PRECISION NOT NULL DEFAULT 0'))

def add_columns(store,table,columns):
    existing=store.columns(table);missing=[column for column in columns if column[0] not in existing]
    for name,definition in missing:store.db.execute(f'ALTER TABLE {table} ADD COLUMN {name} {definition}')
    return bool(missing)

def add_profile_columns(store):return add_columns(store,'profiles',PROFILE_COLUMNS)

def legal_name(name):
    """Old names allowed 20 characters; fit them to the nickname rule (1-10, no control characters)."""
    name=''.join(c for c in str(name) if unicodedata.category(c)[0]!='C').strip()[:NAME_MAX].strip()
    return name or '탐험가'

def fill_name_keys(store):
    """Unique nicknames: the first holder (created_at, id) keeps a name, later ones get 2, 3, ..."""
    rows=store.db.execute('SELECT id,name,name_key FROM profiles ORDER BY created_at,id').fetchall()
    pending=[(pid,legal_name(name)) for pid,name,key in rows if not key]
    if not pending:return False
    taken={key for _,_,key in rows if key};names={}
    for pid,name in pending: # pass 1: names that are still free stay unchanged
        if name_key(name) not in taken:taken.add(name_key(name));names[pid]=name
    for pid,name in pending: # pass 2: suffix, never colliding with a name kept in pass 1
        if pid in names:continue
        suffix=2
        while name_key(name[:NAME_MAX-len(str(suffix))].rstrip()+str(suffix)) in taken:suffix+=1
        names[pid]=name[:NAME_MAX-len(str(suffix))].rstrip()+str(suffix);taken.add(name_key(names[pid]))
    store.db.executemany('UPDATE profiles SET name=?,name_key=? WHERE id=?',[(names[pid],name_key(names[pid]),pid) for pid,_ in pending])
    return True

def add_name_key_index(store):
    if 'profiles_name_key' in store.indexes():return False
    store.db.execute('CREATE UNIQUE INDEX profiles_name_key ON profiles (name_key)')
    store.db.execute('ALTER TABLE profiles ALTER COLUMN name_key DROP DEFAULT')
    return True

def add_run_columns(store):
    added=add_columns(store,'runs',RUN_COLUMNS)
    if 'runs_mode_score' in store.indexes():return added
    store.db.execute('CREATE INDEX runs_mode_score ON runs (mode, score DESC)')
    return True

def set_schema_version(store):
    if store.columns('schema_version') and store.db.execute('SELECT MAX(version) FROM schema_version').fetchone()[0]==SCHEMA_VERSION:return False
    store.db.execute('CREATE TABLE IF NOT EXISTS schema_version (version INTEGER NOT NULL)')
    store.db.execute('DELETE FROM schema_version')
    store.db.execute('INSERT INTO schema_version (version) VALUES(?)',(SCHEMA_VERSION,))
    return True

# Ordered steps; later schema changes append here and must also skip when already applied.
STEPS=(('drop_profile_avatar',drop_profile_avatar),('add_profile_columns',add_profile_columns),
       ('fill_name_keys',fill_name_keys),('add_name_key_index',add_name_key_index),
       ('add_run_columns',add_run_columns),('set_schema_version',set_schema_version))

def migrate(*,database_url=None):
    """Return the names of the steps applied; an empty list means the DB was already current."""
    store=Store(database_url=database_url,initialize=False)
    try:
        version=store.version()
        if version is None:raise ValueError('Empty database; start the server to create it')
        if version>SCHEMA_VERSION:raise ValueError('Database schema is newer than this tool')
        with store.db:applied=[name for name,step in STEPS if step(store)] # PostgreSQL DDL is transactional
        return applied
    finally:store.db.close()

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--database-url',default=os.getenv('IDENTITY_DATABASE_URL'),help='PostgreSQL URL; PG* environment variables are used when omitted')
    args=parser.parse_args()
    try:applied=migrate(database_url=args.database_url)
    except ConfigError as error:raise SystemExit(str(error))
    except Exception as error:raise SystemExit('Migration failed; transaction rolled back. '+type(error).__name__)
    print(json.dumps(dict(applied=applied,up_to_date=not applied)))

if __name__=='__main__':main()
