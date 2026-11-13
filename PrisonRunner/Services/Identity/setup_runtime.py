"""Download pinned official PyPI wheels and verify their published SHA-256 before installing locally."""
import hashlib, io, json, platform, sys, urllib.request, zipfile
from pathlib import Path
BASE=Path(__file__).resolve().parent
if sys.version_info[:2] not in ((3,10),(3,11),(3,12),(3,13)) or platform.machine().lower() not in ('amd64','x86_64'):
    raise SystemExit('Install Python 3.10-3.13 for Windows x64, then run SetupIdentity.ps1 again.')
RUNTIME=BASE/'.runtime';RUNTIME.mkdir(exist_ok=True)
records=[]
for package,version,tag in [('opencv-python-headless','4.13.0.92','cp37-abi3-win_amd64'),('numpy','2.2.6',f'cp{sys.version_info.major}{sys.version_info.minor}-cp{sys.version_info.major}{sys.version_info.minor}-win_amd64')]:
    metadata=json.load(urllib.request.urlopen(f'https://pypi.org/pypi/{package}/{version}/json',timeout=30))
    wheel=next(x for x in metadata['urls'] if tag in x['filename'])
    data=urllib.request.urlopen(wheel['url'],timeout=120).read()
    if hashlib.sha256(data).hexdigest()!=wheel['digests']['sha256']:raise RuntimeError('Wheel checksum failed: '+package)
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        for entry in archive.infolist():
            if not (RUNTIME/entry.filename).resolve().is_relative_to(RUNTIME.resolve()):raise RuntimeError('Unsafe wheel path')
        archive.extractall(RUNTIME)
    records.append(dict(package=package,version=version,filename=wheel['filename'],sha256=wheel['digests']['sha256'],source=wheel['url']))
    print('Verified and installed',package,version,flush=True)
(RUNTIME/'provenance.json').write_text(json.dumps(records,indent=2))
