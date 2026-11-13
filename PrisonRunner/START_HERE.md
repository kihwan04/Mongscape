# Mongscape 처음 실행하기

기준일: **2026-10-29**. 현재 소스는 PR #20의 계정·튜토리얼·게임 규칙과 기존 개선을 합친 버전입니다. 이번 작업은 develop 소스 병합이며 새 게임 ZIP·릴리스·서버 배포는 하지 않습니다.

## 현재 소스로 실행

Unity Hub에서 `Unity/`를 **6000.3.10f1**로 열고 패키지 복원 후 **Mongscape → Play GameScene**을 실행합니다. Windows 실행 파일은 **Mongscape → Build Windows Demo**로 만듭니다. 출력은 `Builds/Mongscape/Windows/Mongscape.exe`입니다.

이 작업 PC에는 통합본의 로컬 빌드를 검수해 두었습니다. 저장소 맨 위 또는 이 폴더의 `StartGame.cmd`는 완료된 빌드를 우선 실행하고 없으면 Unity에서 현재 소스를 엽니다. 다른 PC에서 소스를 새로 받은 경우 빌드를 포함하지 않으므로 Unity 실행/빌드가 필요합니다. 기존 v2026.10.26 공개 ZIP에는 계정 v2 통합 기능이 없습니다.

## 시작과 조작

타이틀 → **얼굴 로그인 / 회원가입 / 게스트 플레이** 선택 → 메인 메뉴 → 게임 시작/튜토리얼 → 플레이 방식·칼로리 목표 → 시작하기 순서입니다. 서버가 없어도 게스트 키보드·몸동작 플레이가 가능합니다. 회원은 서버의 코인·해금·통합 랭킹을 사용하고, 게스트는 이 PC 랭킹만 사용합니다.

| 키 | 동작 |
| --- | --- |
| A/←, D/→ | 레인 이동, 일반 단선 수레 기울이기 |
| C | 갈림길 가운데 선택 |
| Space | 점프 |
| S/↓ | 숙이기 |
| W/↑ | 하이니(연타, 계단에서 번갈아 치기) |
| F1 / F3 | 설정 / 성능 표시 |

몸무게는 비우면 65kg입니다. 표시 칼로리는 입력 몸무게, 점수용 운동량은 65kg 기준이며 키보드는 운동 칼로리가 0입니다. 손 메뉴와 보정은 [몸동작 안내](docs/BODY_TRACKING.md)를 확인하세요.

## 얼굴 계정 서버

얼굴 계정은 **PostgreSQL 전용 서버**가 필요합니다. 아래 예시를 실제 주소로 바꿔 개발 Unity의 `Unity/identity-server.json` 또는 실행 파일 옆 `identity-server.json`에 둡니다.

```json
{"url":"https://faces.your-domain.com"}
```

개발 PC에서는 Unity 메뉴 **Mongscape → Identity Server Address…**에서 주소를 한 번 저장하면 됩니다. `Unity/identity-server.json`(Git 제외)에 저장되고, **Build Windows Demo**가 빌드할 때마다 `Builds/Mongscape/Windows/identity-server.json`으로 복사하므로 환경 변수를 설정하거나 Unity를 다시 켤 필요가 없습니다. 명령줄 빌드는 `-identityUrl https://…` 인수로도 지정할 수 있습니다. 주소가 잘못되면 빌드가 시작 전에 멈춥니다.

환경 변수 `MONGSCAPE_IDENTITY_URL`이 파일보다 우선합니다(Unity Editor는 실행할 때의 환경 변수만 읽습니다). 원격 연결은 HTTPS, 테스트 loopback은 HTTP를 사용합니다. 주소를 바꾼 뒤 게임을 다시 실행합니다. 얼굴 사진은 서버에 보관하지 않고 인식 특징값·계정·기록을 PostgreSQL에 저장합니다.

기존 스키마 v1 서버는 백업 후 계정 v2 이전이 필요합니다. 이전 도구는 자동 실행하지 않으며 이번 작업에서 개인 DB에 적용하지 않았습니다. 구체적인 설정/백업은 [서버 안내](Services/Identity/README_DOCKER.md)를 확인하세요. 문서의 명령은 운영 방법을 설명하며 이번 작업에서 서버를 기동·배포했다는 뜻이 아닙니다.

[기술 스택](docs/TECH_STACK.md) · [진행 상태와 남은 검수](docs/PROGRESS.md) · [문서 목록](docs/README.md)
