# 현재 아키텍처

기준일: 2026-10-29. 몸동작 인식은 Unity 내부에서 실행하고, 선택적 얼굴 로그인과 개인 기록은 별도 Python 서버가 담당합니다. 계층은 필요한 범위의 Clean Architecture Lite를 사용합니다.

## 처리 경로

```text
키보드 ─────────────────────────────┐
웹캠 → GPU 전처리 → BlazePose → 관절 필터·동작 판정 ─┤
                                         MotionCommand → InputRouter → RunSession/RunnerController
                                                              ↓
                                                  캐릭터·맵·카메라·HUD

웹캠 → 얼굴 등록/로그인을 선택했을 때 사진 3장
    → LocalProfileClient → 로컬 HTTP 또는 중앙 HTTPS/Caddy
    → Python 얼굴 API → YuNet/SFace → PostgreSQL 프로필·세션·운동 기록
```

몸동작과 얼굴 로그인은 서로 다른 기능입니다. 중앙 서버가 없어도 키보드와 몸동작 게임은 실행할 수 있습니다.

## Unity 계층

모든 경로는 `Unity/Assets/Game/` 기준입니다.

| 계층 | 주요 코드와 책임 |
| --- | --- |
| `Core/`, `Domain/` | UnityEngine에 의존하지 않는 레인·진행·점수·경로·동작 판정·운동량 규칙 |
| `Application/InputRouter.cs` | 키보드·포즈 명령을 게임 규칙에 전달 |
| `Infrastructure/` | 키보드, 포즈 연결, 로컬 저장, 얼굴 서버 클라이언트 |
| `PoseInference/` | WebCamTexture와 Unity AI Inference 기반 BlazePose GPU 추론 |
| `Presentation/` | 캐릭터·장애물·맵·카메라·UI·오디오 표현 |
| `Content/` | ScriptableObject 설정, 프리팹, 모델과 UI 리소스 |
| `Bootstrap/GameBootstrap.cs` | 구성 요소 생성·연결, 게임 상태와 화면 동기화 |
| `Editor/`, `Tests/` | 씬·빌드 도구와 EditMode/PlayMode 검증 |

게임 규칙은 웹캠이나 HTTP API를 직접 참조하지 않습니다. 입력은 `MotionCommand`로 모으며 키보드 경로를 항상 유지합니다. UI와 외부 연결은 규칙 계층 바깥에 둡니다.

## 게임과 맵

플레이 화면은 `GameScene` 하나를 사용합니다. 정상 시작은 타이틀 → 로그인 선택(회원/게스트) → 메인 메뉴/준비 → 로딩·자세 보정 → 달리기이고, 충돌/추격 → 잡힘 연출 → 결과 → 재시작으로 이어집니다. 씬 전체를 다시 로드하는 방식으로 재시작하지 않습니다.

`RoutePlan`, `RouteSurface`, `CartFork`가 진행 경로와 좌우·높이 좌표를 계산합니다. `ChunkSpawner`가 `MuhanokMapChunk`를 풀에서 꺼내 연결하고, `RuntimeContent`가 차단물·바나나·동물·아이템을 배치/재사용합니다. 현재 확장 경로는 목적지가 섞이는 세 갈래 수레와 광산·감옥·동물원을 포함합니다.

## 얼굴 서버와 저장 경계

`Services/Identity/server.py`는 단일 API 프로세스이고 `storage.py`가 PostgreSQL 연결을 담당합니다(PostgreSQL 전용). 중앙 배포는 Caddy HTTPS → Docker API/Psycopg → PostgreSQL 17 → postgres-data volume 순서입니다. Docker 없이 로컬 Python으로 실행할 때도 PostgreSQL 연결 설정이 필요합니다. 실제 배포 명령과 백업은 [서버 안내](../Services/Identity/README_DOCKER.md)에 있습니다.

서버 주소는 `MONGSCAPE_IDENTITY_URL` → EXE 옆 `identity-server.json` → `http://127.0.0.1:8765` 순서로 정합니다. 원격 주소는 HTTPS를 사용하고 잘못된 설정은 오류로 처리합니다.

| 데이터 | 저장 위치 |
| --- | --- |
| 얼굴 특징값·회원 계정(설정·코인·해금)·로그인 세션·회원 운동 기록 | 중앙 서버 PostgreSQL의 `postgres-data` volume. |
| 이 PC 점수 랭킹 | 게임 PC의 `Player` 로컬 JSON. 기존 Exhibition 파일은 처음 한 번 읽어 이전 |
| 예전 PC 프로필·지갑 | `Player/profile.json`, `wallet.json`. 개발자/전시 모드에서만 읽음 |
| 누적 최고 기록 | `Player/records.json`. 기존 PlayerPrefs 최고 기록도 읽어 보존 |
| 화면/입력 설정 | 게임 PC의 PlayerPrefs |

회원 코인·상점 해금·회원 랭킹·운동 통계는 계정 서버에서 공유합니다. 게스트/이 PC 랭킹과 화면 설정은 각 PC에 남고, 예전 PC 지갑은 개발자/전시 모드에서만 사용합니다. 얼굴 DB·토큰·설치별 설정은 Git/공개 ZIP에서 제외합니다.

패키지 버전은 [기술 스택](TECH_STACK.md), 포즈 판정은 [몸동작 안내](BODY_TRACKING.md), 실제 확인 범위는 [진행 상태](PROGRESS.md)를 확인하세요.

세 갈래 철로는 회차 내 96~120m에서 선택하고 144m부터 다음 맵 입구로 연결됩니다. 갈림길은 48m이며, 확정 후 긴 직선 구간을 제거했습니다. [경로 검수](reports/PR20_LOCAL_MERGE_2026_10_29.md).

## 보존한 입력·성능 기능

`HandMenuPointer`와 `GesturePointer`는 손목 커서의 사용 손 유지·어깨 정규화·0.75초 머무르기를 처리합니다. 얼굴 촬영 카메라와 손 메뉴 옵션을 구분하며 보정 패널 중 손 선택은 중지합니다. `AdaptivePerformance`는 프레임 시간에 따라 렌더 스케일·그림자·SSAO·소품 거리와 포즈 추론 상한을 조절하며 저장 설정은 유지합니다. 동물 목·코와 나무는 풀링된 연속 메시/피벗 연출을 사용합니다.
