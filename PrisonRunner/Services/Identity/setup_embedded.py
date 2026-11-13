"""Bundle the official application-local CPython 3.12 interpreter for the pinned cp312 face runtime."""
from pathlib import Path
import hashlib,io,json,urllib.request,zipfile
BASE=Path(__file__).resolve().parent
url='https://www.python.org/ftp/python/3.12.10/python-3.12.10-embed-amd64.zip'
data=urllib.request.urlopen(url,timeout=60).read()
root=BASE/'python';root.mkdir(exist_ok=True)
with zipfile.ZipFile(io.BytesIO(data)) as archive:
    if archive.testzip() is not None:raise RuntimeError('Python archive CRC failed')
    for member in archive.infolist():
        if not (root/member.filename).resolve().is_relative_to(root.resolve()):raise RuntimeError('Unsafe Python archive member')
    archive.extractall(root)
(root/'python312._pth').write_text('python312.zip\n.\n..\n../.runtime\nimport site\n')
(root/'provenance.json').write_text(json.dumps(dict(version='3.12.10',source=url,sha256=hashlib.sha256(data).hexdigest()),indent=2))
print('Embedded CPython verified and bundled:',len(data),'bytes',flush=True)
