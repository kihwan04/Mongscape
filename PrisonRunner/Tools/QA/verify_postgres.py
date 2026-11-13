"""Run real PostgreSQL tests in a new local cluster; never uses the player's database."""
import argparse
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import sys
import uuid

PROJECT=Path(__file__).resolve().parents[2]
SERVICE=PROJECT/'Services/Identity'
sys.path.insert(0,str(SERVICE/'.runtime'))

def main():
    import psycopg
    parser=argparse.ArgumentParser()
    parser.add_argument('--pg-bin',type=Path,required=True)
    args=parser.parse_args()
    binary=args.pg_bin.resolve(strict=True)
    root=PROJECT/'Builds/QA/PostgresTests'/uuid.uuid4().hex
    root.mkdir(parents=True)
    database=root/'cluster'
    password=secrets.token_hex(32);app_password=secrets.token_hex(32)
    password_file=root/'admin-password.txt';password_file.write_text(password,encoding='ascii')
    with socket.socket() as listener:
        listener.bind(('127.0.0.1',0));port=listener.getsockname()[1]
    env=os.environ.copy();env['PGPASSWORD']=password
    flags=subprocess.CREATE_NO_WINDOW if sys.platform=='win32' else 0
    def run(arguments,background=False,**kwargs):
        if background:
            # A Windows background postgres process can inherit pipe handles. A real log file
            # lets pg_ctl return without waiting for the daemon to close captured stdout.
            with (root/'pgctl-start.log').open('w',encoding='utf-8') as output:
                result=subprocess.run(arguments,env=env,creationflags=flags,stdout=output,
                                      stderr=subprocess.STDOUT,text=True,**kwargs)
            result.stdout=(root/'pgctl-start.log').read_text(encoding='utf-8',errors='replace')
            result.stderr=''
        else:result=subprocess.run(arguments,env=env,creationflags=flags,capture_output=True,text=True,**kwargs)
        if result.returncode:
            print(result.stdout[-3000:]);print(result.stderr[-3000:])
            raise RuntimeError('PostgreSQL QA command failed: '+Path(arguments[0]).name)
        return result
    suffix='.exe' if sys.platform=='win32' else ''
    executable=lambda name:str(binary/(name+suffix))
    version=run([executable('postgres'),'--version']).stdout.strip()
    run([executable('initdb'),'-D',str(database),'-U','postgres','--encoding=UTF8','--locale=C',
         '--auth-host=scram-sha-256','--auth-local=trust','--pwfile',str(password_file)],timeout=90)
    started=False
    try:
        run([executable('pg_ctl'),'-D',str(database),'-l',str(root/'postgres.log'),
             '-o',f'-h 127.0.0.1 -p {port}','-w','start'],background=True,timeout=45)
        started=True
        admin_url=psycopg.conninfo.make_conninfo(host='127.0.0.1',port=port,user='postgres',password=password,dbname='postgres')
        with psycopg.connect(admin_url,autocommit=True) as admin:admin.execute('CREATE DATABASE mongscape')
        sql=(SERVICE/'init-postgres.sh').read_text(encoding='utf-8').split("<<'SQL'\n",1)[1].rsplit('\nSQL',1)[0]
        run([executable('psql'),'-h','127.0.0.1','-p',str(port),'-U','postgres','-d','mongscape',
             '-v','ON_ERROR_STOP=1','-v','app_password='+app_password],input=sql,timeout=15)
        env['MONGSCAPE_TEST_POSTGRES_URL']=psycopg.conninfo.make_conninfo(host='127.0.0.1',port=port,user='mongscape',password=app_password,dbname='mongscape')
        env['PYTHONPATH']=str(SERVICE/'.runtime')+os.pathsep+str(SERVICE)
        result=subprocess.run([sys.executable,'-m','unittest','-v','test_identity','test_http'],cwd=SERVICE,
                              env=env,creationflags=flags,capture_output=True,text=True,timeout=180)
        output=result.stdout+result.stderr
        (root/'test-results.log').write_text(output,encoding='utf-8')
        print(version);print(output)
        record=dict(postgres=version,passed=result.returncode==0,log=str(root/'test-results.log'))
        (PROJECT/'Builds/QA/postgres-results.json').write_text(json.dumps(record,indent=2),encoding='utf-8')
        return result.returncode
    finally:
        if started or (database/'postmaster.pid').exists():
            run([executable('pg_ctl'),'-D',str(database),'-m','fast','-w','stop'],timeout=45)

if __name__=='__main__':raise SystemExit(main())
