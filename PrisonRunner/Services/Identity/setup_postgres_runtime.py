"""Windows PostgreSQL driver for the local face server and tests, downloaded from PyPI and checked against wheel SHA-256."""
import hashlib
import io
import json
from pathlib import Path
import platform
import sys
import urllib.request
import zipfile

BASE=Path(__file__).resolve().parent
if sys.platform!='win32' or platform.machine().lower() not in ('amd64','x86_64'):
    raise SystemExit('Use requirements.txt with pip on Linux/Docker; this helper is Windows x64 only.')
runtime=BASE/'.runtime';runtime.mkdir(exist_ok=True)
tag=f'cp{sys.version_info.major}{sys.version_info.minor}'
packages=[('psycopg','3.3.6','py3-none-any'),('psycopg-binary','3.3.6',f'{tag}-{tag}-win_amd64'),
          ('typing_extensions',None,'py3-none-any'),('tzdata',None,'py2.py3-none-any')]
records=[]
for package,version,wheel_tag in packages:
    url=f'https://pypi.org/pypi/{package}/'+(f'{version}/' if version else '')+'json'
    metadata=json.load(urllib.request.urlopen(url,timeout=30))
    wheel=next(item for item in metadata['urls'] if item['filename'].endswith(wheel_tag+'.whl'))
    data=urllib.request.urlopen(wheel['url'],timeout=60).read()
    digest=hashlib.sha256(data).hexdigest()
    if digest!=wheel['digests']['sha256']:raise RuntimeError('Wheel checksum failed: '+package)
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        if archive.testzip() is not None:raise RuntimeError('Wheel CRC failed: '+package)
        if not all((runtime/name).resolve().is_relative_to(runtime.resolve()) for name in archive.namelist()):
            raise RuntimeError('Unsafe wheel path')
        archive.extractall(runtime)
    records.append(dict(package=package,version=metadata['info']['version'],sha256=digest,source=wheel['url']))
    print('Verified PostgreSQL dependency:',package,metadata['info']['version'],flush=True)
(runtime/'postgres-provenance.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
