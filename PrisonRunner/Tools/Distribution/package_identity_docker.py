"""Prepare central identity deployment and an optional rebuilt Windows client. Never uploads."""
import argparse
import hashlib
import json
import zipfile
from pathlib import Path

PROJECT=Path(__file__).resolve().parents[2]
OUTPUT=PROJECT.parent/'Deliverables'/'Local'/'PR20Merge20261029'
SERVICE=PROJECT/'Services'/'Identity'
SERVER_FILES=(
    'Dockerfile','.dockerignore','requirements.txt','compose.yaml','compose.https.yaml',
    'compose.tailscale.yaml','tailscale-serve.json',
    'Caddyfile','.env.example','README_DOCKER.md','server.py','setup_models.py',
    'models/provenance.json','models/face_detection_yunet.LICENSE','models/face_recognition_sface.LICENSE',
    'ConfigureClient.ps1','identity-server.example.json','test_identity.py','test_http.py','run_tests.ps1',
    'storage.py','schema.postgres.sql','init-postgres.sh','prepare_secrets.py','migrate_account.py',
    'setup_postgres_runtime.py',
)


def write_archive(destination,entries,texts=None):
    with zipfile.ZipFile(destination,'w',zipfile.ZIP_DEFLATED,compresslevel=5) as archive:
        for name,path in sorted(entries.items()):archive.write(path,name)
        for name,value in (texts or {}).items():archive.writestr(name,value)
    with zipfile.ZipFile(destination) as archive:
        if archive.testzip() is not None:raise RuntimeError('Archive CRC failed')
        for name in archive.namelist():
            parts=Path(name).parts
            if any(part in ('data','.runtime','python','__pycache__','secrets') for part in parts):
                raise RuntimeError('Private/runtime directory in deployment archive: '+name)
            if Path(name).name in ('.env','identity-server.json') or name.endswith(('.sqlite3','.db','.log','.dump')):
                raise RuntimeError('Local configuration/data in deployment archive: '+name)
    print(destination.name,destination.stat().st_size,'bytes',flush=True)


def main():
    global OUTPUT
    parser=argparse.ArgumentParser()
    parser.add_argument('--include-windows-client',action='store_true')
    parser.add_argument('--output-directory',type=Path,default=OUTPUT)
    args=parser.parse_args()
    OUTPUT=args.output_directory.resolve()
    if not OUTPUT.is_relative_to(PROJECT.parent):raise RuntimeError('Delivery folder must stay inside the workspace')
    OUTPUT.mkdir(parents=True,exist_ok=True)
    server_entries={name:SERVICE/name for name in SERVER_FILES}
    server_entries['THIRD_PARTY_NOTICES.md']=PROJECT/'THIRD_PARTY_NOTICES.md'
    write_archive(OUTPUT/'Mongscape-Identity-PostgreSQL-Docker.zip',server_entries)
    if args.include_windows_client:
        build=PROJECT/'Builds/Mongscape/Windows'
        if not (build/'Mongscape.complete').is_file():raise RuntimeError('Completed Windows build required')
        if not (build/'Mongscape_Data/Managed/Muhanok.Infrastructure.dll').is_file():
            raise RuntimeError('Expected Mono Windows build missing')
        assembly=(build/'Mongscape_Data/Managed/Muhanok.Infrastructure.dll').read_bytes()
        if b'IdentityServerConfig' not in assembly or 'identity-server.json'.encode('utf-16le') not in assembly:
            raise RuntimeError('Windows build does not contain the remote identity configuration')
        presentation=(build/'Mongscape_Data/Managed/Muhanok.Presentation.dll').read_bytes()
        if b'PlayerProgressStore' not in assembly or b'MainMenuScreen' not in presentation:
            raise RuntimeError('Windows build does not contain the integrated player/menu features')
        if b'RunHudModel' not in presentation:
            raise RuntimeError('Windows build does not contain the compact HUD')
        domain=(build/'Mongscape_Data/Managed/Muhanok.Domain.dll').read_bytes()
        if b'ConnectorChunk' not in domain:
            raise RuntimeError('Windows build does not contain the shortened fork layout')
        entries={}
        for file in build.rglob('*'):
            if not file.is_file():continue
            rel=file.relative_to(build)
            if any('DoNotShip' in part for part in rel.parts):continue
            if file.suffix in ('.log','.pdb') or file.name=='identity-server.json':continue
            entries['Game/'+rel.as_posix()]=file
        for name in ('ConfigureClient.ps1','identity-server.example.json','README_DOCKER.md'):
            entries[name]=SERVICE/name
        entries['THIRD_PARTY_NOTICES.md']=PROJECT/'THIRD_PARTY_NOTICES.md'
        font_license=PROJECT/'Unity/Assets/Game/Content/UI/Resources/MuhanokUI/BlackHanSans-OFL.txt'
        if font_license.is_file():entries['LICENSES/BlackHanSans-OFL.txt']=font_license
        guide='''# Mongscape 로컬 통합 Windows 클라이언트

현재 변경은 로컬 검수 전용이며 업로드·배포하지 않습니다. ZIP 전체를 풀고 StartGame.cmd를 실행합니다. 게임 PC에 Unity·Python·Docker 설치는 필요 없습니다.

타이틀 → 얼굴 로그인/회원가입/게스트 → 메인 메뉴 → 게임 시작/튜토리얼 순서입니다. 계정 서버 없이 게스트 플레이를 사용할 수 있습니다. 회원 코인·해금·통계는 서버, 게스트 기록은 이 PC에 저장합니다. 키보드는 운동 칼로리에 적립하지 않습니다.

회원 기능은 PostgreSQL 전용 얼굴 서버와 Game/identity-server.json의 실제 HTTPS 주소가 필요합니다. 서버를 자동 배포하지 않으며 기존 DB 이전·사진 삭제도 자동 실행하지 않습니다. README_DOCKER.md를 확인하세요. 실제 서버 주소·개인 DB·비밀 설정은 이 파일 묶음에 넣지 않습니다.

A/D 이동, C 갈림길 가운데, Space 점프, S 숙이기, W 하이니, F1 설정, F3 성능 표시. 목표·소모 칼로리, 경찰 사이렌, 연결된 목·코·쓰러지는 나무, 손 커서와 자동 화질 기능을 유지합니다.

점수는 거리 + 65kg 기준 운동 칼로리×100이며 표시 운동량은 입력 몸무게(생략 시 65kg) 기준입니다. 실물 웹캠·외부 서버·여러 PC·다양한 하드웨어 검수는 별도입니다. iOS/Android 카메라는 Windows 웹캠으로 등록하는 외부 연결 방법이 필요합니다.
'''
        launcher='@echo off\r\nsetlocal\r\nstart "" /d "%~dp0Game" "%~dp0Game\\Mongscape.exe" %*\r\n'
        write_archive(OUTPUT/'Mongscape-RemoteClient-Windows-x64.zip',entries,
                      {'StartGame.cmd':launcher,'README_KO.md':guide})
    records=[]
    for file in sorted(OUTPUT.glob('Mongscape-*.zip')):
        with file.open('rb') as stream:digest=hashlib.file_digest(stream,'sha256').hexdigest()
        records.append(dict(file=file.name,bytes=file.stat().st_size,
                            sha256=digest))
    (OUTPUT/'SHA256.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
    guide='''# Mongscape 로컬 통합 파일 안내 (2026-10-29)

현재 변경은 로컬 전용이며 GitHub 업로드·PR 병합·릴리스·외부 서버 배포를 금지합니다. 이 도구는 파일 묶음만 생성하며 서버 시작이나 업로드를 하지 않습니다.

- Windows 클라이언트 ZIP: 전체 압축을 풀고 StartGame.cmd 실행. 최신 빌드가 필요합니다.
- 서버 ZIP: PostgreSQL 17 + 얼굴 API + Caddy/Tailscale 설정과 run_tests.ps1. 실제 배포를 수행한 결과물이 아닙니다.
- SHA256.json: 로컬 ZIP 검증값.

회원 코인·해금·기록·통합 랭킹은 PostgreSQL 계정, 게스트 기록은 PC 랭킹입니다. 서버에 얼굴 사진을 저장하지 않으며 인식 특징값은 보관합니다. 기존 DB의 스키마 v2 이전·사진 삭제는 백업과 별도 확인이 필요하고 이번 로컬 통합에서 실행하지 않았습니다.

현재 검사와 남은 범위는 docs/reports/PR20_LOCAL_MERGE_2026_10_29.md를 기준으로 합니다. 과거 SQLite 검사 결과를 현재 PostgreSQL 전용 서버의 새 검사로 해석하지 않습니다. 실제 Docker/Tailscale·외부 HTTPS·실물 웹캠·여러 PC와 하드웨어는 별도 확인입니다.
'''
    (OUTPUT/'배포_실행_안내.md').write_text(guide,encoding='utf-8')


if __name__=='__main__':main()
