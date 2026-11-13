"""Create local Docker secret files once, without printing or replacing existing passwords."""
from pathlib import Path
import os
import secrets

def prepare(directory):
    directory=Path(directory)
    directory.mkdir(mode=0o700,parents=True,exist_ok=True)
    if os.name!='nt':directory.chmod(0o700)
    for name in ('db_admin_password','db_app_password'):
        path=directory/name
        if path.exists():
            if len(path.read_text().strip())<32:raise ValueError('Existing secret is too short: '+name)
            if os.name!='nt':path.chmod(0o444)
            continue
        # Compose file-backed secrets preserve host modes. The private parent directory protects
        # host access; read-only files let non-root container UIDs read their own mounted secret.
        descriptor=os.open(path,os.O_CREAT|os.O_EXCL|os.O_WRONLY,0o444)
        with os.fdopen(descriptor,'w',encoding='ascii') as stream:stream.write(secrets.token_hex(32)+'\n')
        if os.name!='nt':path.chmod(0o444)
    if (directory/'db_admin_password').read_bytes()==(directory/'db_app_password').read_bytes():
        raise ValueError('Admin and application secrets must differ')
    print('Docker database secret files are ready. Values were not displayed.')

if __name__=='__main__':prepare(Path(__file__).resolve().parent/'secrets')
