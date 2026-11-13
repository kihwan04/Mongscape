# 출시 체크리스트

Mongscape 1.0.0을 GitHub Releases에 ZIP으로 올리기 전에 위에서부터 차례로 확인합니다. 체크리스트는 확인 순서를 정리한 것이며, 릴리스·태그·업로드·서버 배포를 허가하지 않습니다. 각 단계는 그때 따로 승인을 받습니다.

## 브랜치

| 브랜치 | 역할 |
| --- | --- |
| `deploy-test` | 출시 준비 통합 브랜치. 출시용 `feature/release-*` 작업을 여기에 머지합니다. |
| `develop` | 전시용 통합 브랜치. 개발자(전시) 모드를 쓰므로 출시 전용 변경(전시 모드 제거, 빌드 분리)은 넣지 않습니다. |
| `main` | 출시한 안정 버전. 검증을 마친 `deploy-test`를 머지하고 태그를 붙입니다. |

## 1. 문서

- [ ] [개인정보 처리방침](PRIVACY_POLICY.md)의 `[작성 필요]`를 모두 채웠습니다(운영자, 시행일, 보호책임자 연락처, 처리 위탁).
- [ ] 처리방침과 회원가입 동의 문구를 법률 검토로 확인했습니다.
- [ ] [CHANGELOG](../CHANGELOG.md)의 "출시 준비 중"을 출시일로 바꿨습니다.
- [x] 크레딧의 만든 사람 표기를 확정했습니다: bodyLanguage, Monkeys (`Presentation/UI/Overlay/CreditsScreen.cs`).

## 2. 얼굴 서버

- [ ] 운영 서버에 동의 버전 `account-v3` 서버 코드를 배포했습니다. 배포 전 [서버 안내](../Services/Identity/README_DOCKER.md)에 따라 백업을 만듭니다.
- [ ] 백업 보관 기간(`MONGSCAPE_BACKUP_DAYS`, 기본 14일)이 처리방침에 적은 기간과 같습니다.
- [ ] 출시 빌드에 넣을 `identity-server.json` 주소가 운영 서버를 가리킵니다(Unity **Mongscape → Identity Server Address…**).

## 3. 검증

- [ ] `Tools/QA/verify_unity_merge.ps1 -Phase EditMode`, `-Phase PlayMode`가 통과합니다.
- [ ] `Services/Identity/run_tests.ps1`이 통과합니다.
- [ ] `-Phase ReleaseBuild`로 Release 빌드를 만들었습니다. `Builds/Mongscape/Windows/Mongscape.complete` 첫 줄이 `release 1.0.0`입니다.
- [ ] Release 빌드에서 직접 확인했습니다.
  - [ ] 오른쪽 아래 버전이 `v1.0.0`이고 `(Dev)`가 붙지 않습니다.
  - [ ] `Ctrl+Shift+F12`를 눌러도 개발자 모드가 켜지지 않습니다.
  - [ ] 회원가입 동의에서 필수 항목을 모두 체크해야 다음 단계로 넘어갑니다.
  - [ ] 얼굴 서버가 없을 때 게스트 플레이로 끝까지 플레이할 수 있습니다.
  - [ ] 크레딧이 열리고, 로그인 화면과 메인 메뉴의 게임 종료가 동작합니다.
- [ ] `ProjectSettings.asset`에 Unity가 넣은 `SENTIS_ANALYTICS_ENABLED`와 Release 빌드가 남기는 빈 `additionalCompilerArguments` 항목을 커밋하지 않고 되돌렸습니다.

## 4. 패키징

- [ ] `python Tools/Distribution/package_release.py`로 `../Distribution/Mongscape-Windows-x64.zip`과 `SHA256SUMS.txt`를 만들었습니다.
- [ ] ZIP 안에 DB, 얼굴 사진, 토큰, `.env`, Mixamo 원본 FBX, `*_DoNotShip` 폴더가 없습니다. 패키징 도구가 이 파일들과 빌드 PC 폴더 경로를 검사해 막습니다(Release 빌드는 DLL의 디버그 정보 경로를 `Mongscape`로 바꿉니다).
- [ ] ZIP의 `Game/identity-server.json`에는 운영 서버 주소만 있습니다. 주소는 공개되어도 되는 정보이며, 서버는 토큰 인증·HTTPS·요청 제한으로 보호합니다.
- [ ] 다른 PC(또는 새 Windows 사용자)에서 ZIP을 풀고 `StartGame.cmd`로 실행해 봤습니다.

## 5. 출시 (승인 후)

- [ ] `deploy-test`를 `main`에 머지하고 `v1.0.0` 태그를 붙였습니다.
- [ ] `Tools/Distribution/publish_github.ps1 -Commit <main의 전체 SHA> -Tag v1.0.0`으로 GitHub 릴리스를 만들었습니다. 릴리스 본문은 [PUBLIC_RELEASE.md](PUBLIC_RELEASE.md)를 씁니다.
- [ ] 저장소가 비공개이면 ZIP을 받을 수 없습니다. 공개 여부는 직접 정해 GitHub 설정에서 바꿉니다(스크립트는 바꾸지 않습니다).
- [ ] 출시 후 `main`의 변경을 `deploy-test`에도 반영했습니다.

## 다음 버전에서 고려할 것

- **IL2CPP 빌드:** Unity는 출시용 Windows 빌드에 IL2CPP를 권장합니다. C# DLL 대신 네이티브 코드로 나가서 시작이 빠르고 디컴파일이 어렵습니다. Unity Hub의 **Windows Build Support (IL2CPP)** 모듈과 Visual Studio의 **C++를 사용한 데스크톱 개발** 워크로드가 필요하며, 빌드 시간이 길어집니다. 리플렉션을 쓰는 코드(Newtonsoft.Json 등)는 코드 제거 설정을 확인해야 합니다.
- **코드 서명:** 서명하지 않은 `Mongscape.exe`는 처음 실행할 때 Windows SmartScreen이 "Windows의 PC 보호" 경고를 띄웁니다. 코드 서명 인증서(유료)로 서명하면 경고가 줄어듭니다. 그 전까지는 실행 안내에 "추가 정보 → 실행"을 적어 둡니다.
