# Mongscape 개발 프로젝트

**2026-11-14: 1.0.0 정식 출시 준비 중입니다.** 출시 작업은 `deploy-test` 브랜치에 모으고, `develop`은 전시용으로 유지합니다. 출시 순서는 [출시 체크리스트](docs/RELEASE_CHECKLIST.md), 변경 내용은 [CHANGELOG](CHANGELOG.md)를 확인하세요.

Unity 6000.3.10f1 / C# 기반 Windows 운동 러너입니다. 현재 소스는 `leewono/muhanok`의 `6e50060`을 바탕으로 Mongscape 브랜딩, 게임 확장과 Docker 중앙 얼굴 서버 연결을 적용했습니다. 원본 외부 저장소는 수정하지 않습니다.

## 실행과 개발

- 플레이: [START_HERE.md](START_HERE.md). 작업 폴더의 `StartGame.cmd`는 최신 완료 빌드를 실행합니다.
- 개발: Unity Hub에서 `Unity/`를 **6000.3.10f1**로 열고 **Mongscape → Play GameScene**을 실행합니다. `PlayInUnity.cmd`도 같은 씬을 엽니다.
- 빌드: 배포용은 **Mongscape → Build Windows Release**(`Builds/Mongscape/Windows/Mongscape.exe`), QA·캡처용은 **Build Windows Dev (QA)**(`Builds/Mongscape/WindowsDev/Mongscape.exe`, 검수 명령줄 인수는 이 빌드에서만 동작)입니다.
- 검증: `Tools/QA/verify_unity_merge.ps1`, `Tools/QA/verify_short_fork.ps1`. 실행 환경은 [기술 스택](docs/TECH_STACK.md)을 확인하세요.

키보드와 몸동작 플레이는 얼굴 서버 없이 사용할 수 있습니다. 몸동작은 Unity 내부 BlazePose 추론으로 처리하고, 얼굴 로그인·개인 기록은 선택한 로컬/중앙 서버에 저장합니다.

## 폴더

| 위치 | 역할 |
| --- | --- |
| `Unity/` | 씬, 게임 코드, 리소스, 프로젝트 설정 |
| `Services/Identity/` | 얼굴 로그인 API, 모델 준비, Docker·HTTPS 배포 |
| `Tools/` | 에셋 제작, 문서 생성, 테스트와 패키징 |
| `References/` | 제작 원본, 참고 자료, 리소스 출처 |
| `docs/` | 현재 안내, 최신 기획서와 검증 기록 |
| `Builds/` | 로컬 빌드와 검증 산출물. Git 제외 |

[문서 목록](docs/README.md), [아키텍처](docs/ARCHITECTURE.md), [개발 규칙](AGENTS.md), [외부 리소스·공개 조건](THIRD_PARTY_NOTICES.md)을 기준으로 작업합니다. Mixamo 원본 FBX는 공개 소스에서 제외하며 자체 Blender 동작으로 대체 연결합니다.

저장소는 `kihwan04/Mongscape`(예전 이름 `kihwan04/PrisonRunner`)입니다. 이전 [v2026.10.26 릴리스](https://github.com/kihwan04/Mongscape/releases/tag/v2026.10.26)는 계정 v2 이전의 전시용 데모입니다. 1.0.0 릴리스 본문은 [PUBLIC_RELEASE.md](docs/PUBLIC_RELEASE.md)입니다. 회원 개인정보 처리는 [개인정보 처리방침](docs/PRIVACY_POLICY.md)을 따릅니다.

현재 계정·메뉴·상점·모드별 랭킹·튜토리얼은 [게임 화면 안내](docs/GAME_FLOW_UI.md)를 확인하세요. 초기 UI는 `lilohdong/muhanok-test` main `b2215ef`, 계정 v2와 게임 규칙은 PR #20 head `6880a33`을 바탕으로 기존 로컬 개선과 합쳤습니다.
