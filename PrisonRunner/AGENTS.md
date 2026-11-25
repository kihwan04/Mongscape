# 프로젝트 작업 규칙

## 현재 기준

- 게임 이름은 **Mongscape(몽스케이프)**, 이전 이름은 무한옥이다. 내부 Muhanok/PrisonRunner 네임스페이스, GUID, 저장 키와 과거 문서 제목은 기존 참조를 유지한다.
- 작업 대상은 이 체크아웃과 개인 저장소 `kihwan04/PrisonRunner`다. 별도 `leewono/muhanok` 저장소는 수정하지 않는다. 현재 소스는 `6e50060` 기반이며 이전 구현으로 되돌리지 않는다.
- **2026-10-29 최신 사용자 지시: PR #20 통합 소스 및 미사용 문서·이미지·리소스 정리를 develop에 반영하는 것만 승인한다. 새 릴리스·태그·실행 파일/영상 게시·서버 배포와 기존 개인 DB 마이그레이션은 금지한다. 이전의 업로드 금지는 이 소스 병합 범위에 한해 변경됐다.**
- 새 Windows 빌드의 기본 경로는 `Builds/Mongscape/Windows/Mongscape.exe`다. StartGame은 완료된 새 빌드를 우선하고 없으면 현재 소스를 Unity에서 실행한다. 이전 Muhanok.exe로 돌아가지 않는다.

## 먼저 읽을 문서

- [문서 목록](docs/README.md), [진행 상태](docs/PROGRESS.md), [다음 계획](docs/DEVELOPMENT_PLAN.md)
- [아키텍처](docs/ARCHITECTURE.md), [아트 기준](docs/ART_DIRECTION.md)
- 몸동작은 [BODY_TRACKING](docs/BODY_TRACKING.md), 얼굴 서버는 [Docker 안내](Services/Identity/README_DOCKER.md)

최신 화면별 기획서는 `docs/무한옥_화면별_게임기획서_V13.docx`이며 이후 게임 구현과 구분한다. 이전 버전 문서·V10~V12 기획서·과거 캡처는 2026-10-27에 삭제했으며 Git 기록에서만 복원한다. 과거 기록의 숫자·통신 방식·미완료 상태를 현재 명세로 적용하지 않는다.

## 개발 원칙

- 기존 코드를 먼저 확인하고 기능 단위로 변경한다. 정상 기능의 불필요한 전체 리팩터링과 씬 재생성을 하지 않는다.
- Unity **6000.3.10f1**과 기존 패키지 버전을 임의로 업그레이드하지 않는다. 불필요한 패키지를 설치하지 않는다.
- Domain/Core는 UnityEngine 없이 순수 규칙으로 유지한다. Application/Infrastructure/Presentation과 책임을 분리하고 MonoBehaviour 하나에 여러 책임을 몰지 않는다.
- 게임 규칙은 웹캠·키보드·HTTP 구현을 직접 참조하지 않는다. 공통 MotionCommand로 연결하고 키보드 플레이를 항상 유지한다.
- 몸동작은 Unity AI Inference의 BlazePose와 MotionClassifier가 담당한다. Python PoseServer·UDP·WebSocket을 다시 도입하지 않는다. 사람이 안 보일 때 무효 입력과 재보정을 처리한다.
- GameScene 하나를 사용하며 재시작에 scene reload를 사용하지 않는다. 현재 게임 흐름과 경로 규칙은 코드와 최신 보고서를 확인한다.
- 청크/장애물은 풀링하고 안전한 차선을 유지한다. Entry/Exit·레인·경로 높이 연결과 풀 반환 시 상태 복원을 확인한다.
- Visual Mesh와 Collider를 분리한다. 모델 교체가 게임 규칙을 바꾸지 않게 한다. 설정은 가능한 ScriptableObject로 관리한다.
- UI Toolkit 설정창의 요소 name은 `docs/SETTINGS_UI.md`를 따른다. F1 설정과 F3 성능 표시를 유지한다.
- 기능과 가독성을 그래픽 장식보다 우선한다. 실제 적용·자체 제작·후보·미임포트를 구분하며 상용 benchmark 품질 달성을 근거 없이 주장하지 않는다.

## 얼굴 서버·데이터

- 중앙 서버는 Caddy HTTPS → Docker Python/OpenCV/Psycopg API → PostgreSQL 17 volume 구성이다. 몸동작 추론은 각 게임 PC에서 유지한다.
- 주소 우선순위는 MONGSCAPE_IDENTITY_URL → EXE 옆 identity-server.json → 기존 loopback 기본값이다. 원격 HTTPS와 잘못된 설정의 오류 처리를 유지한다.
- 얼굴 등록 동의 화면은 연결된 서버 저장을 안내한다. 실제 DB·얼굴·토큰·.env·설치별 설정을 Git/공개 ZIP에 넣지 않는다.
- Docker build context는 .dockerignore 허용 목록으로 제한한다. 기존 로컬 DB를 중앙 서버에 자동 업로드하지 않는다.
- 기존 2026-10-25 ZIP은 localhost 전용이다. 중앙 주소 설정에는 2026-10-26 새 클라이언트를 사용한다.

## 리소스·Git·문서

- 무료 에셋과 라이선스 조건을 지킨다. 새 리소스의 크기·피벗·재질·콜라이더를 검수한다. AI 생성 리소스는 Staging에서 확인한다.
- Mixamo 원본 FBX는 공개 재배포에서 제외한다. 원본/출처는 `THIRD_PARTY_NOTICES.md`를 따른다.
- main은 발표 가능한 안정 버전, develop은 통합, 기능은 feature/*에서 작업한다. 세부 규칙은 `docs/GIT_WORKFLOW.md`를 따른다.
- 커밋 하나에 목적 하나를 담고 브랜치/메시지에는 기능과 변경 목적만 쓴다. 작성 도구/생성 주체 문구와 자동 서명을 넣지 않는다.
- Library·Temp·Logs·빌드·비밀 설정을 커밋하지 않는다. Unity 소스와 대응 .meta를 함께 관리한다.
- 문서는 현재 안내와 과거 기록을 분리한다. 문서를 옮기면 상대 링크와 문서 생성/패키징 도구의 경로도 수정한다. 라이선스·출처 파일을 불필요 문서로 삭제하지 않는다.

## 종료 보고

생성·수정 파일, 구현 기능, 수행한 검증과 Compile Error 여부, 남은 문제와 다음 작업을 보고한다. 과거 검사 결과를 새로 수행한 결과처럼 쓰지 않는다.

- 추가 UI 통합 기준은 `lilohdong/muhanok-test` main `b2215ef589e028c2a7efce6af814ac7f02e2ce55`다. 메뉴/상점/거리 랭킹은 `docs/GAME_FLOW_UI.md`를 따른다. 얼굴 서버/PostgreSQL과 기존 확장 경로를 유지한다. 코인·해금은 회원 계정(중앙 DB)에만 쌓이고, 게임 PC에는 PC 랭킹(게스트·회원 판)만 남는다(예전 PC 지갑은 읽지 않는다). 둘을 혼동하지 않는다.
