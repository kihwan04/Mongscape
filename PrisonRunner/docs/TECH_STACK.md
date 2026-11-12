# Mongscape(이전 이름: 무한옥) 실행 구조와 기술 스택

2026-10-29 통합 소스와 고정 패키지 설정을 기준으로 한 안내입니다. 이전 기획 문서의 Python PoseServer/WebSocket 구조 대신 아래 구조를 사용합니다.

**중앙 서버 구성 추가:** 여러 PC가 하나의 Docker 얼굴 서버에 HTTPS로 연결하는 배포 구성을 추가했습니다. [Docker 배포 안내](../Services/Identity/README_DOCKER.md). 실제 외부 서버와 도메인은 아직 배포하지 않았습니다. Docker 없이 로컬 Python을 실행하는 경우에도 PostgreSQL 연결 설정이 필요합니다.

## 실행 방법

### 바로 플레이

기존 `Mongscape-RemoteClient-Windows-x64.zip`은 통합 이전 실행본입니다. 현재 기능은 이 브랜치를 Unity 6000.3.10f1로 실행하거나 직접 빌드해 확인합니다. 직접 만든 Windows 빌드는 `StartGame.cmd`로 실행합니다. Windows 64비트용이며 게임 PC에는 Unity·Python·Docker 설치가 필요 없습니다. `Game` 폴더 전체를 보관합니다. 얼굴 서버는 별도 서버에 배포합니다. 전달 파일 경로는 [처음 읽기](../START_HERE.md)를 확인하세요. GitHub의 기존 공개 ZIP은 과거 버전입니다.

개발 폴더에서는 루트 `StartGame.cmd` → `PrisonRunner/StartGame.cmd` 순으로 연결됩니다. 얼굴 서버가 준비돼 있으면 먼저 시작하고, 완료 표시가 있는 `Builds/Mongscape/Windows/Mongscape.exe`를 실행합니다. 빌드가 없으면 `PlayInUnity.cmd`로 현재 Unity 소스를 엽니다. 이전 Muhanok 빌드로 돌아가지 않습니다.

타이틀 → 얼굴 로그인/회원가입/게스트 선택 → 메인 메뉴 → 게임 시작/튜토리얼 → 탈출 준비에서 전신/상체/키보드와 칼로리 목표 선택 → 시작하기 순서입니다. 얼굴 등록/로그인은 선택 사항입니다. A/D 또는 좌우 화살표로 이동, Space로 점프, S/아래로 숙이기, W/위로 하이니(연타)를 합니다. F1은 설정, F3은 성능 표시입니다.

### 소스 개발

Unity Hub에서 **Unity 6000.3.10f1**로 `PrisonRunner/Unity`를 엽니다. 패키지 복원과 임포트를 기다린 뒤 `GameScene`을 실행합니다. 얼굴 로그인을 사용하려면 계정 v2 PostgreSQL 서버가 필요합니다. 로컬 서버는 `SetupIdentity.cmd`로 런타임/모델을 준비하고 PostgreSQL 연결 설정 뒤 `StartIdentity.cmd`를 실행합니다. 기존 스키마의 자동 이전은 하지 않습니다. 키보드와 몸동작 플레이는 얼굴 서버 없이도 사용할 수 있습니다.

공개 소스는 Mixamo 원본 FBX를 제외하고 자체 애니메이션 대체 경로를 제공합니다. 복원 방법과 외부 리소스 조건은 [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md)를 확인하세요.

## 기술 스택

| 영역 | 기술과 고정 버전 | 역할 |
| --- | --- | --- |
| 엔진/언어 | Unity 6 6000.3.10f1, C# | Windows 게임과 편집 도구 |
| 렌더링 | Universal Render Pipeline 17.0.1 | 3D 그래픽, 조명, 후처리 |
| 입력 | Input System 1.18.0, WebCamTexture | 키보드와 웹캠 영상 |
| AI 추론 | Unity AI Inference 2.6.1 | GPU에서 BlazePose ONNX 추론 |
| 포즈 모델 | BlazePose detector, full/lite landmark 모델 | 사람 영역과 33개 신체 관절 추정 |
| 영상 처리 | Compute Shader, One Euro 필터 | GPU 전처리와 관절 떨림 보정 |
| 카메라 | Cinemachine 3.1.7, 자체 카메라 리그 | 1인칭/수레/잡힘 연출 |
| 시퀀스 | Timeline 1.8.10, Animator/Humanoid | 인트로와 캐릭터 애니메이션 |
| UI | UI Toolkit(UXML/USS), C# 화면 코드 | 설정, 준비, HUD, 결과, 얼굴 로그인 |
| 맵 제작 | ProBuilder 6.1.2, 자체 런타임 메시 생성 | 청크, 터널, 갈림길과 연결로 |
| 얼굴 서버 | CPython 3.12(Windows 휴대용 3.12.10), 표준 라이브러리 HTTPServer | 로컬 얼굴 로그인 또는 Docker 중앙 API |
| 서버 배포 | Docker Compose, Caddy 2 | HTTPS 진입점, 컨테이너와 영구 DB volume |
| 중앙 DB·드라이버 | PostgreSQL 17, Psycopg 3.3.6 | 회원 설정·코인·해금·세션·운동 기록, BYTEA 인식 특징값과 JSONB 활동 기록 |
| 얼굴 인식 | opencv-python-headless 4.13.0.92, NumPy 2.2.6, YuNet/SFace | 얼굴 검출, 정렬, 특징 벡터 비교 |
| 로컬 저장 | JSON/PlayerPrefs | 이 PC 점수 랭킹·화면 설정·예전 개발자용 프로필/지갑 |
| 리소스 | Blender, Mixamo, Kenney, 자체 생성 재질 | 캐릭터, 애니메이션, 환경 |
| 도구/검증 | Python, PowerShell, Unity Test Framework/NUnit | 리소스 제작, 패키징, 규칙/플레이/서버 검증 |

버전 근거는 `Unity/ProjectSettings/ProjectVersion.txt`, `Unity/Packages/manifest.json`, `Services/Identity/setup_*.py` 및 배포 런타임의 provenance 파일입니다. 패키지는 이번 공개 준비에서 업그레이드하지 않았습니다.

## 웹캠 몸동작 처리

1. Unity `WebCamTexture`로 영상을 받습니다.
2. `PoseImageSampler.compute`가 GPU에서 모델 입력 영상을 만듭니다.
3. BlazePose 검출기로 사람 영역을 찾고 랜드마크 모델로 33개 관절을 추정합니다. 추적 중에는 직전 결과를 이용하고 놓쳤을 때 검출기를 다시 사용합니다.
4. One Euro 필터로 좌표를 안정화하고 시작 자세를 보정합니다.
5. `Domain/MotionClassifier.cs`가 전신/상체 방식에 맞게 좌우 구역, 점프, 숙이기, 하이니(좌우 번갈아 `KneeStep`)와 활동 시간을 판정합니다.
6. `Infrastructure/Pose/PoseInput.cs`가 공통 `MotionCommand`를 전달하고 `RunnerController`가 캐릭터를 움직입니다. 키보드도 같은 명령 경로를 사용합니다.

전신 방식은 실제 몸 위치와 점프/무릎 번갈아 들기(`KneeStep`)를 사용합니다. 상체 방식은 상체 좌우 이동, 두 손 들기, 숙이기, 팔 번갈아 크게 흔들기를 사용합니다. 이 처리는 **Unity 내부**에서 실행되며 Python PoseServer나 외부 클라우드 AI 호출이 필요하지 않습니다.

## 얼굴 로그인 처리와 저장 위치

게임은 사용자가 얼굴 등록/로그인을 선택하면 시차를 둔 얼굴 이미지 3장을 연결된 얼굴 서버로 보냅니다. 기본값은 `http://127.0.0.1:8765`이고, EXE 옆 `identity-server.json` 또는 환경 변수 `MONGSCAPE_IDENTITY_URL`로 원격 HTTPS 주소를 지정합니다. YuNet이 한 명의 얼굴을 검출하고 SFace가 특징 벡터를 만들어 저장된 벡터와 비교합니다. 로그인한 얼굴 사진은 캐릭터 머리에 표시되고 운동 기록은 해당 프로필에 연결됩니다.

얼굴 서버는 PostgreSQL만 사용하며 Docker 중앙 DB는 PostgreSQL 17의 `postgres-data` volume에 저장합니다. 서버 테스트는 `Services/Identity/run_tests.ps1`이 만드는 임시 PostgreSQL에서 실행합니다. 중앙 서버는 Psycopg로 연결하고 DB 비밀번호는 서버의 Docker secret 파일로 관리합니다. 특징 벡터, 이름, 세션과 운동 기록이 들어가므로 Git/공개 ZIP에서 제외합니다. 얼굴 사진은 DB에 저장하지 않고, 로그인·가입 요청의 첫 장에서 잘라낸 얼굴을 응답으로만 돌려줍니다. 로그인 토큰은 서버에서 해시로 보관합니다. 게임의 얼굴 삭제 기능으로 프로필과 관련 기록을 지울 수 있습니다. 로컬 기본 서버는 loopback에서만 접속하고, Docker 중앙 서버는 Caddy의 HTTPS 진입점을 사용합니다.

이 기능은 전시용 프로필 인식이며 검증된 사진 위조 방지 기능은 없습니다. 몸동작 인식과 얼굴 로그인은 서로 다른 처리 경로입니다.

## 게임 내부 구조

`Domain/Core`는 규칙, `Application`은 진행 상태, `Infrastructure`는 키보드/포즈/저장/얼굴 서버 연결, `Presentation`은 캐릭터/맵/UI/사운드를 담당합니다. `Bootstrap/GameBootstrap.cs`가 이 구성 요소를 연결합니다. 맵 청크와 장애물은 풀에서 재사용하고, 세 갈래 수레 경로와 광산/감옥/동물원 경로를 이어 생성합니다. 효과음과 배경 음악은 코드로 합성합니다.

## 확인 범위

2026-10-29 통합본: C# 11개 어셈블리 컴파일 오류 0, EditMode 178/178, PlayMode 74/74, 실제 임시 PostgreSQL 17.11 서버 27/27 통과. Windows 빌드·19장 UI 캡처·기본 플레이/120청크/충돌/결과/재시작 검사를 완료했습니다. 공개 소스의 애니메이션은 재배포 제한이 없는 자체 Blender 동작으로 연결합니다.

실물 웹캠·본인 얼굴·휴대폰, 외부 HTTPS·Docker/Tailscale·두 PC 공유, 다양한 하드웨어 성능은 미검증입니다. 자동 캡처 종료에서 기존 Windows 콜백 오류가 한 차례 재현됐고 검수 종료 절차를 정리한 최종 검사는 정상 종료했습니다. 실제 개인 DB 이전과 서버 배포는 수행하지 않았습니다. [자세한 검사·한계](reports/PR20_LOCAL_MERGE_2026_10_29.md) · [현재 상태](PROGRESS.md).
