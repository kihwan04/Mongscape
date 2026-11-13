from pathlib import Path
import hashlib, json, urllib.request
BASE=Path(__file__).resolve().parent
MODELS={'face_detection_yunet':'face_detection_yunet_2023mar.onnx','face_recognition_sface':'face_recognition_sface_2021dec.onnx'}
manifest=BASE/'models'/'provenance.json'
pinned={entry['file']:entry for entry in json.loads(manifest.read_text())}
records=[]
for folder,filename in MODELS.items():
    sha=pinned[filename]['sha256']
    size=pinned[filename]['size']
    target=BASE/'models'/filename; target.parent.mkdir(exist_ok=True)
    if not target.exists() or hashlib.sha256(target.read_bytes()).hexdigest()!=sha:
        url=f'https://media.githubusercontent.com/media/opencv/opencv_zoo/main/models/{folder}/{filename}'
        data=urllib.request.urlopen(url,timeout=180).read()
        if len(data)!=size or hashlib.sha256(data).hexdigest()!=sha: raise RuntimeError('Model checksum mismatch')
        target.write_bytes(data)
    license=urllib.request.urlopen(f'https://raw.githubusercontent.com/opencv/opencv_zoo/main/models/{folder}/LICENSE',timeout=60).read()
    (target.parent/(folder+'.LICENSE')).write_bytes(license)
    records.append(dict(file=filename,sha256=sha,size=size,source=f'https://github.com/opencv/opencv_zoo/tree/main/models/{folder}'))
    print('Verified',filename,size,flush=True)
(BASE/'models'/'provenance.json').write_text(json.dumps(records,indent=2))
