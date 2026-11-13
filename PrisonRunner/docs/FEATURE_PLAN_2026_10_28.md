# 2026-10-28 기능 구현 계획

이 문서는 PR #20의 2026-10-28 작업 명세를 보존한 자료입니다. 당시 코드·테스트·수치와 일부 계획 표현은 현재 구현과 다를 수 있으므로 현재 동작은 [게임 UI](GAME_FLOW_UI.md), 실제 검증은 [진행 상태](PROGRESS.md)를 기준으로 확인합니다.

이 문서는 작업 명세서다. 작업(T1~T15)마다 브랜치, 선행 작업, 현재 코드, 바꿀 내용, 완료 조건을 적었다. 작업 하나를 맡은 사람은 **0장 공통 규칙**, **1장 확정 결정**, 그리고 자기 작업 절을 읽으면 바로 시작할 수 있다. 서버 API는 **4장**에만 정의하고, 각 작업은 4장을 참조한다.

- 코드 경로는 `Unity/Assets/Game/` 기준, 서버 경로는 `Services/Identity/` 기준이다(둘 다 저장소의 `PrisonRunner/` 아래).
- 줄 번호는 `develop` `4c4f6d2` 기준이다. 작업 전에 해당 위치를 다시 확인한다.

---

## 0. 공통 규칙

### 브랜치와 PR
1. `git fetch origin` → `git switch -c <브랜치> origin/develop`
2. 커밋 → `git push -u fork <브랜치>` (`fork` = lilohdong/PrisonRunner-fork)
3. `gh pr create --repo kihwan04/PrisonRunner --base develop --head lilohdong:<브랜치>`

- 커밋 하나에 목적 하나를 담는다. 메시지는 `<유형>(<범위>): <변경 목적>` 형식이고, 유형은 `feat`, `fix`, `chore`, `docs`, `refactor`, `test` 중 하나다.
- 커밋 메시지와 PR에 작성 도구 표시나 자동 서명(Co-Authored-By 등)을 넣지 않는다 (`AGENTS.md`, `docs/GIT_WORKFLOW.md`).
- Unity 소스는 대응하는 `.meta`와 함께 커밋한다. `Library`·`Temp`·`Logs`·빌드·비밀 설정은 커밋하지 않는다.

### 코드 구조 규칙 (AGENTS.md 요약)
- `Domain`/`Core`는 UnityEngine을 참조하지 않는 순수 규칙이다. 게임 규칙은 웹캠·키보드·HTTP 구현을 직접 참조하지 않는다.
- 계층은 Application / Infrastructure / Presentation으로 나눈다. MonoBehaviour 하나에 여러 책임을 몰지 않는다.
- GameScene 하나만 쓰고 scene reload를 하지 않는다. 청크·장애물·코인은 풀링하며, 풀 반환 시 상태를 복원한다.
- 키보드 플레이는 항상 동작해야 한다. 몸동작은 BlazePose와 MotionClassifier만 쓴다(Python PoseServer, UDP, WebSocket 금지).
- Unity 6000.3.10f1과 패키지 버전을 올리지 않는다. 새 패키지를 설치하지 않는다.
- 주변 코드의 스타일(밀집된 한 줄 작성, 짧은 영어 주석, 한국어 UI 문자열)을 따른다.

### 테스트 실행
- **Unity**: Unity Editor를 닫은 상태에서 실행한다.
  ```powershell
  powershell -File Tools/QA/verify_unity_merge.ps1 -Phase EditMode
  powershell -File Tools/QA/verify_unity_merge.ps1 -Phase PlayMode
  ```
  - 결과는 `Builds/QA/MuhanokTestMerge/<Phase>-latest.json`에 남는다. Compile Error가 있으면 로그의 `error CS` 줄이 출력된다.
  - 테스트 위치: `Tests/EditMode/*.cs`, `Tests/PlayMode/*.cs`
- **서버**: `Services/Identity/.runtime`이 있어야 한다(`SetupIdentity.cmd`로 준비).
  - `test_identity.py`, `test_http.py`: SQLite로 실행된다. `test_identity.py`는 테스트 이미지를 인터넷에서 내려받는다.
  - `test_postgres.py`: PostgreSQL이 필요하다(`README_DOCKER.md`).

### PR 체크리스트
- [ ] EditMode/PlayMode(서버 작업이면 Python) 테스트 결과와 Compile Error 여부
- [ ] 새 동작을 검증하는 테스트 추가
- [ ] 관련 문서 갱신(`docs/GAME_FLOW_UI.md`, `docs/BODY_TRACKING.md`, `docs/SETTINGS_UI.md`, `Services/Identity/README_DOCKER.md` 중 해당하는 것)
- [ ] UI 변경 시 1280×720 화면 캡처
- [ ] 실행하지 않은 검증을 실행한 것처럼 쓰지 않는다

---

## 1. 확정 결정 (2026-10-28)

| 항목 | 결정 |
| --- | --- |
| 계정 | 얼굴 계정이 회원 계정이다. 플레이어는 **회원**과 **게스트** 두 종류다 |
| 첫 화면 | 타이틀 → 로그인 선택(얼굴 로그인 / 회원가입 / 게스트 플레이) |
| 회원가입 | 저장 동의 → 닉네임(중복 확인) → 몸무게·기본 방식(선택) → 얼굴 촬영 |
| 닉네임 | 회원은 가입할 때 정하고, 이후에도 중복 확인을 거쳐 변경할 수 있다 |
| 로그인 유지 | 게임을 끄면 로그아웃한다. 자동 로그인은 없다 |
| 회원 랭킹 | 서버 통합 랭킹(모든 PC의 회원 기록) |
| 게스트 | 판이 끝난 뒤 결과 화면에서 닉네임을 입력하면 **그 PC 랭킹에만** 기록된다. 건너뛰면 "게스트"로 기록한다 |
| 코인·아이템 | 코인은 회원 계정에 쌓이는 재화다. **상점에서 해금한 아이템만** 플레이 중에 나온다. 게스트는 코인이 쌓이지 않고 아이템도 나오지 않는다 |
| 아이템 효과 | 자석은 실제로 코인을 끌어당긴다. 2배 점수는 "코인 2배"로 바꾼다. 방패는 미끄러짐·바나나 추격까지 막는다 |
| 얼굴 사진 | 서버에 저장하지 않는다. 로그인할 때 찍은 얼굴을 서버가 잘라 응답으로만 돌려주고, 클라이언트가 뱃지로 붙인다 |
| 로그인 캠 | 얼굴 점 표시(서버 `/detect`)는 하지 않는다. 몸 골격을 빼고 고정된 얼굴 가이드 틀을 그린다 |
| 몸무게 | 입력하지 않으면 **65kg**으로 계산한다. 몸무게 미입력으로 칼로리 계산을 빼는 경우는 없앤다 |
| 점수 | **점수 = 거리(m) + 소모 칼로리 × 100**. 코인·장애물 통과는 점수에 넣지 않는다. 모든 랭킹은 점수로 정렬한다(키보드는 칼로리가 0이라 점수 = 거리)(T13) |
| 결과 화면 | 점수, 플레이 시간(표시만), 거리, **내 최고 기록**(회원, 모드별)과 신기록 여부를 보여 준다 |
| 얼굴 서버 DB | **PostgreSQL만 쓴다.** SQLite 경로와 SQLite 가져오기 도구를 없앤다. 로컬 SQLite 서버를 쓰는 PC와 배포된 SQLite 데이터는 없다(T15) |
| 장애물 배치 | 판마다 씨드로 **모든 구간의 장애물을 랜덤 배치**한다(광산 철로의 끊어진 레일 포함). 첫 구간은 점프 줄과 숙이기 줄 중 하나를 랜덤으로 고른다. 구간 순서(경로)는 지금처럼 고정한다(T14) |

**하지 않기로 한 것**
- 아이템 처음부터 생성, 상점을 지속시간 강화로 개편
- 결과 화면에 "다음 해금까지 남은 코인" 표시
- 튜토리얼의 아이템 체험 단계
- 서버 `/detect`와 YuNet 얼굴 점 표시

---

## 2. 작업 순서

| 작업 | 브랜치 | 내용 | 선행 | 겹치는 파일 |
| --- | --- | --- | --- | --- |
| T1 | `feature/ingame-cam-preview` | 인게임 우측 하단 캠 | - | `RunHud.cs`(T3) |
| T2 | `feature/fullbody-crouch-tuning` | 전신 숙이기 오인식 수정 | - | - |
| T3 | `feature/powerup-effects` | 자석·코인 2배·방패 효과 | T1 | `RunHud.cs`(T1), `RunRules.cs`(T13) |
| T13 | `feature/run-score` | 점수 공식·몸무게 기본값·플레이 시간 | T3 | `RunRules.cs`, `WorkoutEstimate.cs`, `RunHud.cs` |
| T14 | `feature/random-obstacles` | 장애물 랜덤 배치(첫 구간·광산 철로 포함) | T3 | `MuhanokMapChunk.cs`, `RuntimeContent.cs`(T3), 광산 구간 프리팹 |
| T4 | `feature/identity-transient-avatar` | 얼굴 사진 서버 저장 제거 | - | `server.py`, `storage.py`(T5) |
| T5 | `feature/account-server-api` | 계정 스키마·API·마이그레이션 | T4 | - |
| T15 | `feature/identity-postgres-only` | 얼굴 서버 PostgreSQL 단일화(SQLite 제거) | T5 | `storage.py`, `server.py`, 서버 테스트 |
| T6 | `feature/identity-face-guide` | 로그인 캠 골격 제거 + 얼굴 가이드 틀 | - | `HudData.cs`(T1) |
| T7 | `feature/account-session` | 클라이언트 계정 구조(화면 변경 없음) | T5 | `GameBootstrap.cs` |
| T8 | `feature/login-gate-signup` | 로그인 선택·회원가입 화면 | T6, T7 | `HUDPresenter.cs` |
| T9 | `feature/member-profile-shop` | 내 정보·회원 상점·게스트 닉네임 | T8 | `LeaderboardScreen.cs`(T10) |
| T10 | `feature/ranking-by-mode` | 모드별 랭킹(회원/PC)·칼로리 합계·내 최고 기록 | T9, T13 | `LeaderboardScreen.cs` |
| T11 | `feature/result-calibration` | 결과 화면 웹캠 보정 | T10 | `LeaderboardScreen.cs` |
| T12 | `feature/tutorial` | 튜토리얼 | T2, T8 | - |

- 작업 번호는 만든 순서이고, 진행 순서는 이 표의 위에서 아래다(T13은 T3 다음).
- T1→T3→T13, T3→T14, T4→T5, T6은 서로 독립이라 동시에 진행할 수 있다(T13과 T14는 서로 겹치는 파일이 없다). 겹치는 파일이 있는 작업은 표의 순서대로 머지한 뒤 rebase한다.
- 서버 PR(T4, T5)이 머지되어도 중앙 서버 적용은 T7~T10이 준비된 뒤 한 번에, **별도 확인을 받고** 진행한다. 그 전까지 클라이언트는 로컬 Docker 서버(`compose.yaml`) 또는 `StartIdentity.cmd` 로컬 서버로 개발한다.

---

## 3. 작업 명세

### T1. 인게임 우측 하단 캠

**현재 코드**
- `Presentation/UI/RunHud.cs:43-44`: `Model.ShowPoseRecovery && data?.CameraTexture != null`일 때만 `PosePreview.Draw(skin, new Rect(1020, 490, 236, 174), data, true)`를 호출한다.
- `Presentation/UI/RunHudModel.cs:66-69`: `ShowPoseRecovery`는 카메라 모드에서 인식이 0.75초 이상 끊겼을 때만 참이다.
- 정상 추적 중에 캠을 숨기는 것은 의도된 동작이었다(`docs/GAME_FLOW_UI.md`).

**변경**
1. `RunHud`: 카메라 모드(`PlayStyleInfo.UsesCamera`, 키보드가 아님)이고 설정이 켜져 있으면 항상 우측 하단에 캠을 그린다.
   - 정상 추적 중에는 약 200×150 크기로, `showStatus: false`(영상과 골격만)로 그린다.
   - `ShowPoseRecovery`가 참이면 지금의 복구 표시(236×174, `showStatus: true`)로 바꾼다.
   - 하단 안내 영역(`RunHud.cs:13`, x 400–880)과 일시정지 버튼 등 우측 UI와 겹치지 않게 배치한다.
2. 설정 토글을 추가한다.
   - `Domain/UserSettings.cs`에 `public bool InGameCamera = true;`
   - `SettingsWindow.uxml` 표시 탭(`page-display`)에 토글 `ingame-camera`
   - `Presentation/UI/SettingsWindow.cs`에서 연결
   - `docs/SETTINGS_UI.md`의 요소 이름 목록에 추가
3. `docs/GAME_FLOW_UI.md`의 "정상 추적 중에는 웹캠 미리보기를 숨긴다" 문장을 새 동작으로 고친다.

**완료 조건**
- 전신·상체 모드: 플레이 중 캠이 항상 보인다.
- 키보드 모드나 설정을 끈 경우: 캠이 보이지 않는다.
- 인식이 끊기면 복구 표시로 바뀐다.

**테스트**: `Tests/EditMode/RunHudTests.cs:46-48` 갱신. 표시 조건(모드×설정×인식 상태) EditMode 테스트 추가.

---

### T2. 전신 모드 숙이기 오인식 수정

**원인** (`Domain/MotionClassifier.cs`)
- 전신 숙이기는 **어깨 Y 하강**만으로 판정한다.
  - `drop = (ShoulderY - bSy) / 몸통길이`이고, `drop > CrouchDrop(0.26)`가 `CrouchConfirmSeconds(0.12초)` 유지되면 숙이기다 (`:150`, `:164`, `UpdateCrouch :215-221`).
  - 엉덩이·무릎·코는 보지 않는다.
- 점프 직전에 무릎을 굽히면 어깨가 0.26 넘게 내려가 0.12초 안에 숙이기로 확정된다. 그 뒤:
  - `Reading.Crouch` 상태에서는 점프가 막힌다 (`:156`).
  - 숙이기가 풀릴 때 `Lock(RefractorySeconds=0.3)`이 걸려 점프가 다시 막힌다 (`:217`).
- 낮은 점프는 `hipRise`가 `JumpRise(0.15)`에 못 미쳐 `airborne`이 켜지지 않는다. 그래서 `LandingGraceSeconds(0.3)` 유예 없이 착지 자세가 숙이기로 판정된다 (`:163`).
- 값은 `Domain/PoseTypes.cs:71` 등(`:58-77`)과 `Content/Data/GameSettings.asset:56-86`에 함께 있다. 런타임은 `PoseTracker`가 복사본을 쓴다(`Infrastructure/Pose/PoseInput.cs:109`).

**변경** (Domain 수정 + 설정값. 상체 모드(`TrackUpper`)는 건드리지 않는다)
1. 기준값: `CrouchDrop` 0.26 → 0.40, `CrouchConfirmSeconds` 0.12 → 0.20. 해제 기준(현재 진입값×0.65)은 비율을 유지한다. `PoseTypes.cs`와 `GameSettings.asset`을 함께 바꾼다.
2. 점프 우선: 숙이기가 확정된 지 0.35초 이내에 점프 상승(`up`)이 감지되면 숙이기를 취소하고 점프를 발생시킨다. 이 취소에는 `Lock`을 걸지 않는다.
3. 착지 유예 확대: 어깨와 엉덩이의 상승 속도가 빠른 직후(점프 시도)에도 `airborne`이 아니어도 `LandingGraceSeconds`를 적용한다.
4. 새 값은 `PoseTypes`의 설정 필드로 둔다(하드코딩 금지). `docs/BODY_TRACKING.md`의 기준값 표를 갱신한다.

**완료 조건과 테스트** (`Tests/EditMode/BodyTrackingAndExhibitionTests.cs`에 합성 랜드마크 시퀀스로 추가)
- 무릎 굽힘 0.25초 → 점프: 숙이기 0회, 점프 1회
- 낮은 점프(`hipRise` 0.10) 후 착지: 숙이기 0회
- 실제 숙이기(drop 0.5) 0.5초 유지: 숙이기 1회
- 기존 테스트가 모두 통과한다.

**범위 밖**: 실제 웹캠 실측. PR에 "F3 진단값으로 실측 확인 필요"를 남긴다.

---

### T3. 아이템 효과 (자석·코인 2배·방패)

**현재 코드**
- **생성**: `Presentation/Map/RuntimeContent.cs:146-156`. 걷는 청크마다 35% 확률로 하나를 만든다. `availableItems()`(`GameBootstrap.cs:82`에서 `Store.Wallet.Available()`)에 있는, 즉 **해금한 아이템만** 생성한다. 이 규칙은 유지한다.
- **획득**: `ItemPickup.Evaluate`(`RuntimeContent.cs:15-23`). 아이템과 같은 z를 지나는 프레임에 좌우 1.0 이내면 `run.Items.Activate(Kind)`.
- **타이머**: `Domain/PowerUps.cs:27-41`. 자석 8초, 2배 10초, 방패 15초이고, 다시 먹으면 처음 값으로 리셋된다.
- **자석**: `Presentation/Map/CoinController.cs:10-19`
  - 코인과 같은 z를 지나는 프레임에만 판정하고(`:13`), 자석이면 좌우 범위를 0.8 → 3.4, 높이 범위를 0.9 → 3.0으로 넓힐 뿐이다(`:17`).
  - 레인 간격이 2.2(`MuhanokMapChunk.cs:105`)라서 바로 옆 레인까지만 먹는다. 끌어오는 움직임은 없다.
- **2배 점수**: `Domain/RunRules.cs:70-81, 237`. 점수만 2배로 하는데, 점수(`Score.Score`)를 표시하는 화면이 없다. 코인 개수는 그대로다.
- **방패**: `RunRules.cs:252-263`. `CaptureReason.Collision`만 막는다. 미끄러짐(`Slip :265-273`), 바나나 추격(`:245`), 레일 끊김(`CartRailGap.cs:17`)은 막지 못한다. 캐릭터에 표시가 없다.
- **표시**: HUD 남은 시간 라벨(`RunHudModel.cs:52-58`, `RunHud.cs:34-39`), 효과음(`Presentation/Audio/GameAudio.cs:61-62`).

**변경**
1. **자석이 실제로 끌어당기게 한다** (`CoinController`)
   - 자석이 켜져 있으면 플레이어 앞 8m 안에 있고 아직 먹지 않은 코인을 레인과 관계없이 매 프레임 플레이어 위치 쪽으로 이동시킨다. 일정 거리(약 0.8) 안에 들어오면 획득한다.
   - 이동 로직은 Presentation에 둔다. `ChunkSpawner.Evaluate`(`ChunkSpawner.cs:141`) 흐름을 유지한다.
   - **풀링 복원**: 코인의 원래 `localPosition`을 저장하고 `ResetCoin()`에서 복원한다. `MuhanokMapChunk.Prepare`(`:101-106`)는 x만 다시 정하므로, 이 처리가 없으면 끌려간 y·z가 다음 청크에 남는다.
   - 자석이 켜진 동안 캐릭터 주변에 간단한 효과를 표시한다.
2. **2배 점수를 코인 2배로 바꾼다**
   - `ItemKind.DoubleScore`의 enum 값과 이름은 저장 호환을 위해 유지한다. `ItemCatalog`의 표시 이름을 "코인 2배", 설명을 "잠시 동안 코인을 두 배로 얻습니다."로 바꾼다.
   - 켜져 있는 동안 `ScoreSystem.CollectCoin()`이 코인을 2개 더한다. 점수 배수(`Multiplier`)는 T13에서 점수 공식과 함께 정리하므로 여기서는 건드리지 않는다.
   - HUD 코인 숫자 옆에 "×2"를 표시한다.
3. **방패**
   - `RunSession.Slip()`에서 방패가 있으면 방패를 소모하고 미끄러짐과 바나나 카운트를 막는다. `ShieldSaves++`로 효과음이 나게 한다. 바나나 추격도 이 경로에서 시작되지 않는다.
   - 레일 끊김(`BrokenRail`)은 막지 않는다.
   - 방패가 켜진 동안 캐릭터 주변에 반투명 보호막을 표시하고, 막은 순간 깨지는 연출(짧은 스케일·페이드)을 넣는다.
   - 설명을 "15초 동안 충돌·미끄러짐을 한 번 막아 줍니다."로 바꾼다.
4. **배치**: 아이템을 활성 장애물이 있는 레인에 두지 않고, 바나나 줄(`DynamicFloorItems`)과 겹치지 않게 한다 (`RuntimeContent.cs:152`, `FreeZ :179-188`).
5. **상점 안내**: `ShopScreen`의 아이템 설명 아래에 "해금하면 플레이 중에 등장합니다"를 표시한다.

**완료 조건과 테스트**
- PlayMode: 해금한 아이템만 생성된다(기존 동작 유지).
- PlayMode: 자석이 켜지면 반대쪽 끝 레인 코인도 획득하고, 꺼지면 획득하지 못한다.
- PlayMode: 자석으로 끌려간 코인이 풀 반환 뒤 원래 위치로 돌아온다.
- EditMode: 코인 2배 중 `CollectCoin` 1회에 코인 +2
- EditMode: 방패가 충돌과 미끄러짐을 1회 막고 소모된다. 레일 끊김은 막지 않는다.
- 기존 `ItemsDoubleScoreShieldOneHitAndExpire` 테스트를 새 의미에 맞게 고친다.

---

### T13. 점수 공식 · 몸무게 기본값 · 플레이 시간

**현재 코드**
- **점수**(`Domain/RunRules.cs:66-83` `ScoreSystem`): `거리×10 + 장애물 통과 보너스 + 코인×10`(2배 점수 중에는 배수)이다. 화면에는 표시되지 않고, 랭킹은 거리로 정렬한다(`Ranking.cs:67`).
- **칼로리**(`Domain/WorkoutEstimate.cs`): `MET × 3.5 × 몸무게 / 200 × 운동초 / 60`
  - MET는 플레이 방식별 고정값이다: 전신 4, 상체 2.5, 키보드 1.3 (`Ranking.cs:24-25`, `GameBootstrap.cs:184, 194`).
  - **운동초(`ActiveSeconds`)**는 카메라 모드에서 인식이 되고 실제 운동 동작이 감지된 시간만 센다(`GameBootstrap.cs:244`, `WorkoutEstimate.cs:25-29, 44-48`). 키보드 모드는 0이다.
  - 몸무게가 없으면 `WeightKg=null`이 되어 칼로리가 0이다(`:14, 32`). 화면은 "칼로리 계산 제외"로 표시한다(`LeaderboardScreen.cs:41, 90-92`, `PauseScreen.cs:21-22`, `RunHudModel.cs:40`).
- **플레이 시간**: `RunSession`의 `clock`(`RunRules.cs:223, 234`)이 일시정지를 빼고 흐르지만 private이고 화면에 나오지 않는다.

**변경**
1. **몸무게 기본값 65kg**
   - `WorkoutEstimate.DefaultWeightKg = 65`를 추가한다.
   - `SetWeight("")`(빈 값)는 65를 쓴다. `WeightKg`는 null이 아닌 `double`로 바꾼다.
   - "몸무게 미입력 · 칼로리 계산 제외" 분기를 모두 없앤다(`LeaderboardScreen.cs:41, 90`, `PauseScreen.cs:21-22`, `RunHudModel.cs:40`). 키보드 모드의 "칼로리 계산 제외"는 유지한다.
   - 프로필·준비 화면의 몸무게 칸 안내를 "미입력 시 65kg으로 계산"으로 바꾼다.
2. **점수 공식**: `점수 = round(거리(m) + 점수용 칼로리 × 100)`
   - 점수용 칼로리 = `MET × 3.5 × 65 / 200 × 운동초 / 60`. **입력한 몸무게가 아니라 65kg 고정**으로 계산한다. 같은 운동을 해도 몸무게가 무거운 사람이 점수를 더 받지 않게 하기 위해서다. 화면에 보이는 칼로리는 입력한 몸무게로 계산한다.
   - 예: 전신 모드, 1,240m, 3분 운동 → 1,240 + 13.65 × 100 = 2,605점. 같은 거리라도 운동 동작이 적으면 점수가 낮다.
   - 키보드 모드는 운동초가 0이라 점수 = 거리다. 모든 모드가 같은 공식을 쓰고, 모든 랭킹은 점수로 정렬한다.
   - 코인, 장애물 통과 보너스, `Multiplier`는 점수에서 뺀다. `CleanPass`는 콤보 기록용으로만 남긴다.
   - 공식은 `Domain`의 순수 함수(`RunScore.From(distance, WorkoutEstimate)`)로 두고 EditMode로 검증한다. 게임 중에는 매 프레임 같은 함수로 현재 점수를 계산한다.
3. **점수 버전**: `RankingEntry`에 `ScoreVersion`(새 공식 = 2)을 추가한다. 이전 공식 점수는 비교할 수 없으므로 랭킹은 버전 2 기록만 순위에 넣는다(서버도 같은 규칙, 4장). 이전 기록은 지우지 않는다.
4. **플레이 시간**(표시만, 점수와 무관): `RunSession.Elapsed`(일시정지 제외 플레이 초)를 공개한다. 결과 화면 요약 줄과 일시정지 화면에 `플레이 3:12` 형식으로 표시한다.
5. **표시**
   - HUD 오른쪽 위 정보 묶음(`RunHud`)에 점수를 가장 크게 표시하고, 거리를 그 아래에 둔다.
   - 결과 화면 제목 숫자(`LeaderboardScreen.cs:65`, 현재 거리)를 점수로 바꾸고, 거리는 요약 줄로 옮긴다.
   - 일시정지 화면에 점수를 추가한다.
6. `Leaderboard` 정렬(`Ranking.cs:67`)을 점수 기준(동점이면 먼저 세운 기록)으로 바꾼다. 탭 UI는 T10에서 만든다.

**완료 조건과 테스트**
- EditMode
  - 몸무게 빈 값 → 65kg, 칼로리 > 0
  - 점수 = 거리 + 점수용 칼로리 × 100 (반올림)
  - 같은 거리·운동초에서 몸무게가 달라도 점수가 같다.
  - 같은 거리에서 운동초가 많을수록 점수가 높다. 키보드 점수 = 거리
  - 랭킹이 점수로 정렬되고, 버전 1 기록은 순위에서 빠진다.
  - `Elapsed`가 일시정지 중에 늘지 않는다.
- 기존 점수 관련 테스트(`BodyTrackingAndExhibitionTests.cs:190-197` 등)를 새 공식에 맞게 고친다.
- `docs/GAME_FLOW_UI.md`에 점수 공식과 몸무게 기본값을 적는다.

---

### T14. 장애물 랜덤 배치 (첫 구간 · 광산 철로 포함)

**현재 코드**
- **씨드**: 판마다 새 씨드를 뽑는다(`GameSettings.asset` `RandomizeLayout: 1`, `GameBootstrap.cs:197`). 씨드는 `RoutePlan`, `ChunkSpawner`의 `System.Random`으로 전달된다(`ChunkSpawner.cs:79-88`).
- **구간 순서는 고정**: 확장 경로(`ExtendedRoutes: 1`)의 첫 바퀴는 `0, 1, 2, 1, 2, 1, 3, 7, …`이다(`Domain/RoutePlan.cs:95`). 이 작업에서도 순서는 바꾸지 않는다.
- **구간 프리팹**(`Content/Prefabs/CH_*.prefab`)에는 장애물이 미리 놓여 있다.
  - `ExerciseObstacles`: 레인 3개를 다 막는 운동 줄. Crate(점프)·Pipe(숙이기) 등이고, 구간마다 2줄(6개)이다. 계단 구간은 3개다.
  - `Obstacles`: 레인 하나를 막는 장애물 6개(2줄 × 3레인)
  - 감옥 복도의 `Gates`, 계단의 `SlideGates`, `FloorItems`
  - 광산 직선 구간(`CH_Mine_Straight_A/B`, 타입 1·2)의 `RailGaps` 2개(z 8, 18)
- **배치 규칙**(`Presentation/Map/MuhanokMapChunk.cs:52-100` `Prepare`)
  - 운동 줄은 광차 구간이 아니면 **항상 켜진다**. 첫 구간(순번 0)에서만 Crate 줄을 끈다(`:64-66`). 그래서 **매 판 첫 장애물은 레인 전체를 막는 숙이기 줄**이다. 주석("clear warm-up")과 실제 동작이 다르다.
  - 레인 장애물: 타입 4~6의 첫 줄만 랜덤 레인(`:82-83`), 그 밖은 확률 줄(`:95-98`)
- **추가 배치**(`Presentation/Map/RuntimeContent.cs` `Decorate`)
  - 걷는 구간(0, 4, 5, 12)은 72%(동물원 52%) 확률로 미리 놓인 장애물을 끄고 레인 막이로 바꾼다. 바나나와 동물원 동물도 여기서 놓는다.
  - 첫 구간과 광차 구간에서는 일찍 반환한다.
- **광산 철로**: 끊어진 레일 위치·방향이 고정이다.
  - `CartRailGap`(`Presentation/Map/CartRailGap.cs`)은 판정만 한다. 레일이 끊긴 모양은 **에디터 도구가 다리 메시를 만들 때 레일을 빼고 구워 넣은 것**이다(`Editor/MuhanokArtUpgrade.Cart.cs:61-72`, 끊긴 쪽은 `RouteSurface.CartMissingSide`, `Domain/RouteSurface.cs:37-41`).
  - 그래서 게임 중에 `RailGap`을 끄기만 하면 레일은 끊겨 보이는데 통과되는 불일치가 생긴다. 갈림길 구간은 이미 이렇게 끄고 있다(`ChunkSpawner.cs:119`).

**변경**
1. **배치 결정을 Domain으로 뺀다**: `Domain/ObstacleLayout.cs`(UnityEngine 미참조)
   - 입력: 씨드에서 나온 `System.Random`, 구간 타입, 순번, 구간이 가진 장애물 슬롯 목록(종류·줄·레인)
   - 출력: 켤 슬롯 목록과 광산 레일 상태
   - `MuhanokMapChunk.Prepare`와 `RuntimeContent.Decorate`는 이 결과대로 켜고 끈다. 같은 씨드면 항상 같은 배치다.
2. **첫 구간**: 순번 0의 `CH_Mine_Start`에서 점프 줄(Crate)과 숙이기 줄(Pipe) 중 **하나를 랜덤으로** 남긴다. `:64-66`의 워밍업 규칙을 대체한다.
3. **걷는 구간 운동 줄**
   - 구간의 운동 줄마다 켤지 말지를 랜덤으로 정한다. 구간당 최소 1줄은 켠다.
   - 계단(타입 7)의 하이니 계단은 지형의 일부이므로 항상 켠다.
   - 같은 위치의 종류 바꾸기(Crate↔Pipe)는 모델이 달라 이번 범위에서 하지 않는다. 대신 켜지는 줄의 조합이 판마다 달라진다.
4. **레인 장애물**: 모든 걷는 구간에서 줄마다 확률(`Difficulty.ObstacleChance`)로 랜덤 레인 하나를 막는다. `SafePatternValidator`로 줄마다 최소 1레인을 비워 둔다. 감옥 복도 `Gates`도 같은 방식으로 랜덤 레인에 켠다.
5. **광산 철로**
   - 레일 끊김 2곳(z 8, 18)마다 판마다 랜덤으로 정한다: 없음 / 왼쪽 끊김 / 오른쪽 끊김. 구간당 최소 1곳은 끊는다. 갈림길 구간은 지금처럼 끊김 없음.
   - **레일 모양을 실행 중에 바꿀 수 있게 한다**
     - 에디터 도구(`MuhanokArtUpgrade.Cart.cs`)가 z 8, 18 부근(±1.5m)의 레일을 **양쪽 다 빼고** 굽게 바꾼다.
     - 실행 중에는 풀링한 레일 조각(`SingleRail`·`RailFastener`와 같은 크기와 재질)을 끊기지 않을 쪽에 채운다.
     - `CartRailGap.RequiredLean`은 고른 방향에 맞게 설정한다.
   - 프리팹 재생성은 Unity 배치 모드에서 해당 에디터 메서드로 실행한다. 바뀐 `.prefab`과 `.meta`를 함께 커밋한다.
   - `RouteSurface.CartMissingSide`는 고정값 대신 이 판의 배치를 돌려주도록 바꾸거나, 에디터 전용으로 남긴다.
6. **안전 규칙**(EditMode 테스트로 검증)
   - 어떤 줄도 레인 3개를 모두 막지 않는다(운동 줄은 동작으로 통과).
   - 구간 이음새 앞뒤 4m 안에는 장애물을 두지 않는다(지금 바나나 규칙과 같음).
   - 서로 다른 동작이 필요한 장애물 사이 간격을 최소 6m 둔다.
7. **문서**: `docs/GAME_FLOW_UI.md`에 배치 규칙을 적는다.

**완료 조건과 테스트**
- EditMode(`ObstacleLayout`)
  - 같은 씨드면 같은 배치, 다른 씨드 100개에서 배치가 여러 가지로 나온다.
  - 씨드 500개에서 첫 구간이 점프와 숙이기를 모두 포함한다(각각 30% 이상).
  - 씨드 500개 × 모든 구간 타입에서 안전 규칙 위반이 없다.
  - 광산 구간에 끊김이 최소 1곳 있고, 방향이 왼쪽·오른쪽 모두 나온다.
- PlayMode
  - 광산 구간에서 끊김 없음으로 정해진 곳은 레일이 이어져 보이고(레일 조각 활성) 통과해도 잡히지 않는다.
  - 끊김으로 정해진 곳은 반대로 기울이지 않으면 잡힌다.
  - 풀 반환 후 다시 쓰일 때 레일 조각이 새 배치대로 바뀐다.
- 기존 테스트(`CollisionAndCartTests`, `StartAndGateTests` 등)에서 고정 배치를 가정한 것은 씨드를 고정해 고친다.

---

### T4. 얼굴 사진 서버 저장 제거 (서버)

**현재 코드**
- `server.py:20-34` `extract`: YuNet 검출 → `alignCrop`(112×112) → SFace 특징값. 정렬된 얼굴을 JPG로 만들어 함께 반환한다.
- `server.py:62-71`: 가입할 때 첫 장 JPG를 `profiles.avatar`에 넣는다.
- `storage.py:97-100` `profile()`: DB의 avatar를 base64로 응답에 넣는다. 로그인 판정은 특징값만 쓴다(`server.py:42-52`).

**변경**
1. `/login`·`/enroll` 응답의 `avatar`는 **이번 요청 첫 장의 정렬 얼굴**(`samples()`가 이미 돌려주는 값)로 채운다. DB에는 넣지 않는다.
2. `storage.py`: `profile(profile_id, token, avatar)`가 avatar를 인자로 받는다. SELECT에서 avatar를 뺀다.
3. 스키마: `schema.sql`, `schema.postgres.sql`에서 `avatar` 열을 삭제하고 INSERT 문을 맞춘다. `migrate_sqlite.py:23`의 열 목록에서도 avatar를 뺀다.
4. 기존 DB 변환은 T5의 마이그레이션 스크립트에 포함한다(`DROP COLUMN avatar`). T4 단독 머지 시에도 기존 DB가 깨지지 않도록, T4에서 `migrate_account.py`의 첫 단계로 avatar 삭제를 넣고 T5에서 확장한다.
5. 동의 문구: `Unity/.../Presentation/UI/IdentityScreen.cs:26`의 "얼굴 특징과 얼굴 사진, 플레이 기록을 저장합니다"를 "얼굴 특징값과 플레이 기록을 저장합니다. 얼굴 사진은 저장하지 않습니다."로 바꾼다. `README_DOCKER.md`의 저장 항목 설명도 고친다.
6. 클라이언트 파싱(`LocalProfileClient.cs:86-90`)은 바꾸지 않는다.

**완료 조건과 테스트**
- `test_identity.py`: 로그인·가입 응답에 avatar가 있고, `profiles`에 avatar 열이 없다.
- `test_http.py:26`, `test_postgres.py:102`의 avatar 사용을 고친다.
- 마이그레이션: avatar가 있는 기존 SQLite DB를 변환한 뒤 로그인이 된다.

**주의**: 중앙 DB의 기존 사진 삭제는 되돌릴 수 없다. PR에는 코드와 스크립트만 넣고, 중앙 서버에서는 실행하지 않는다.

---

### T5. 계정 스키마·API·마이그레이션 (서버)

**현재 코드**
- `server.py`: `/enroll`, `/login`, `/logout`, `/delete`, `/runs`, `/history`, `GET /health`가 있다. 요청을 하나씩 처리하는 `HTTPServer`다.
- 가입·로그인 시도 제한은 모든 PC 공용으로 1분 15회다(`:58-60`).
- `profiles`: id, name, avatar, created_at, consent_version. 이름 중복을 허용하고 변경 API가 없다.
- `runs`: 모드·코인이 없다.

**변경**: 4장의 스키마와 API를 그대로 구현한다.
1. **스키마**: `schema.sql`(SQLite)과 `schema.postgres.sql`(PostgreSQL)을 4.1의 새 스키마로 바꾼다. `schema_version` 테이블을 추가한다(값 2).
2. **마이그레이션**: `migrate_account.py`(T4에서 시작)
   - SQLite 경로 또는 PostgreSQL URL을 받아 v1 → v2로 변환한다. 이미 v2면 아무것도 하지 않는다.
   - 단계: avatar 삭제 → profiles 새 열 추가 → `name_key` 채우기(중복은 뒤에 2, 3…을 붙여 정리) → UNIQUE 인덱스 → runs 새 열 → `schema_version=2`
   - 전체를 한 트랜잭션으로 처리한다(SQLite는 `DROP COLUMN`을 위해 3.35 이상 필요. 안 되면 테이블 재생성).
3. **서버 시작 검사**: `Store` 초기화 때 `schema_version`이 2가 아니면 "migrate_account.py를 먼저 실행하세요"로 종료한다. 새 DB는 바로 v2로 만든다.
4. **API**: 4.2의 경로를 `App.handle`에 추가한다.
   - 닉네임 규칙과 아이템 가격은 4.3을 따른다.
   - 시도 제한 한도는 모듈 상수로 뺀다. `/name-check`는 별도 한도(1분 60회)를 둔다.
5. **문서**: `README_DOCKER.md`에 마이그레이션 실행 순서(백업 → 스크립트 → 재시작), 새 API, 코인 값을 클라이언트가 보낸다는 한계를 적는다.

**완료 조건과 테스트** (SQLite·PostgreSQL 양쪽)
- 각 API의 정상 경로와 오류 경로(인증 없음, 닉네임 중복·규칙 위반, 코인 부족, 이미 해금, 잘못된 모드)
- 같은 기록 ID로 `/runs`를 두 번 보내도 코인은 한 번만 적립된다.
- `/leaderboard`가 사람당 최고 거리 1개만 순위에 넣고, 오늘 필터가 KST 기준으로 자른다.
- v1 DB(이름 중복 포함)를 마이그레이션하면 v2가 되고 기존 회원이 로그인된다.

---

### T15. 얼굴 서버 PostgreSQL 단일화 (서버)

**배경**: `Store`가 PostgreSQL(중앙 Docker)과 SQLite(로컬 `StartIdentity`) 두 저장소를 지원해서, 서버를 고칠 때마다 SQL·마이그레이션·테스트를 두 벌로 맞춰야 했다. 기본 테스트도 SQLite로 돌아 운영 환경(PostgreSQL)과 달랐다. 로컬 SQLite 서버를 쓰는 PC와 배포된 SQLite 데이터는 없다(2026-10-28 확인).

**변경**
1. `storage.py`, `server.py`에서 SQLite 분기와 `--database` 옵션을 없앤다. PostgreSQL 설정이 없으면 분명한 오류로 멈춘다.
2. `schema.sql`, `migrate_sqlite.py`를 삭제하고, `migrate_account.py`의 SQLite 분기를 없앤다.
3. 로컬 실행 스크립트(`StartIdentity`, `SetupIdentity`)는 SQLite로 기본 실행하지 않고 PostgreSQL 설정을 요구한다. 기본 경로는 Docker(`compose.yaml`)다.
4. 테스트를 PostgreSQL 하나로 합친다. `run_tests.ps1`이 휴대용 PostgreSQL 17을 임시로 띄워(관리자 아닌 앱 계정 `mongscape`) 테스트를 돌리고 끈다.
5. `README_DOCKER.md`와 SQLite를 언급한 문서를 갱신한다.

**완료 조건**: 저장소에서 SQLite 런타임 경로가 사라지고, `run_tests.ps1`로 전체 서버 테스트가 PostgreSQL에서 통과한다.

---

### T6. 로그인 캠: 골격 제거 + 얼굴 가이드 틀

**현재 코드**
- `Presentation/UI/IdentityScreen.cs:25`: `PosePreview.Draw(skin, new Rect(70,130,560,400), data, true)`로 캠 영상과 **BlazePose 몸 골격**, 몸동작 상태를 그린다.
- `PosePreview`(`Presentation/UI/HudData.cs:41-100`)가 영상(좌우 반전) → 골격(`:61-76`) → `showStatus`면 상태·보정 막대 순서로 그린다.

**변경**
1. `PosePreview.Draw`에 매개변수 `bool drawSkeleton = true`를 추가한다. 기본값이 true이므로 다른 호출부는 그대로 둔다.
2. `IdentityScreen`은 `drawSkeleton: false, showStatus: false`로 영상만 그린다.
3. 얼굴 가이드 틀을 그린다.
   - 영상 가운데에 타원 테두리를 그린다. 높이는 영상 높이의 60%, 너비는 높이의 0.75배다.
   - 바깥 영역은 반투명하게 어둡게 한다.
   - 틀 아래에 "틀 안에 얼굴을 맞춰 주세요"를 표시한다.
   - `IdentityBusy` 동안 틀 색을 강조색으로 바꾸고 문구를 "가만히 계세요"로 바꾼다.
   - 타원은 IMGUI에서 점 또는 짧은 선분을 반복해 그린다(`PosePreview.Dot`과 같은 방식). 새 텍스처 에셋을 만들지 않는다.
4. 손 가리키기 선택은 그대로 둔다. BlazePose는 계속 돌고 커서는 `GesturePointer.Draw()`가 그린다.

**완료 조건**
- 로그인 화면에 골격과 몸동작 상태가 보이지 않고, 가이드 틀이 보인다.
- 다른 화면(설정 캠, 인게임 캠, 결과 화면)은 기존대로 골격을 그린다.
- 손 가리키기로 로그인 버튼을 누를 수 있다.

**테스트**: PlayMode에서 `IdentityScreen`이 열린 상태로 한 프레임 그려도 예외가 없는지 확인한다(기존 `MergedMenuPlayModeTests` 패턴 사용).

---

### T7. 클라이언트 계정 구조 (화면 변경 없음)

**현재 코드**
- PC 저장소: `Infrastructure/PlayerProgressStore.cs`에 `profile.json`(이름·몸무게·방식·목표), `wallet.json`(코인·해금), `records.json`(랭킹·통계)이 있다.
- 서버 통신: `Infrastructure/LocalProfileClient.cs`(`Authenticate`, `SignOut`, `SaveRun`)
- `Bootstrap/GameBootstrap.cs`가 현재 플레이어를 `Store.Profile`, `identity.Profile`, `player` 세 곳에서 섞어 읽는다 (`:71, 82, 110, 303-304, 350-358, 396, 411, 438-448, 456`).

**변경**
1. **`AccountSession`** (Application, `Application/AccountSession.cs`, UnityEngine 미참조)
   - `Kind`(Guest/Member), `Id`, `Nickname`, `Settings`(몸무게, 기본 방식, 목표), `Coins`, `Unlocked`(ItemKind 집합), `TutorialDone`
   - `SignInMember(profile)`, `SignOut()`(게스트로 전환), `Changed` 이벤트
   - 게스트의 설정은 세션 메모리에만 둔다.
2. **`IAccountGateway`** (Application 인터페이스)와 구현 `LocalProfileClient`(Infrastructure)
   - 4.2의 API를 코루틴/콜백 방식으로 노출한다: `Login`, `Enroll`, `NameCheck`, `FetchProfile`, `UpdateProfile`, `SaveRun`, `Buy`, `Leaderboard`, `Stats`, `Logout`, `Delete`
   - 응답 파싱은 지금처럼 `JsonUtility`를 쓴다. 기존 사진 3장 촬영(`LocalProfileClient.cs:61-82`)은 재사용한다.
   - 테스트용 가짜 구현(`FakeAccountGateway`)을 테스트 어셈블리에 둔다.
3. **`GameBootstrap`이 `AccountSession`만 읽게 바꾼다.**
   - 아이템 생성 조건(`:82`)은 회원이면 `AccountSession.Unlocked`, 게스트면 빈 목록을 쓴다. 개발자 모드는 `UnlockAll`을 유지한다.
   - 기록 저장(`:346-358`)
     - 회원: `SaveRun`(4.2의 `/runs` 필드 전부: mode, coins, play_seconds, score_version 포함) 성공 시 응답의 코인 잔액을 `AccountSession`에 반영한다. PC 랭킹에도 `AccountId`와 함께 기록한다.
     - 게스트: PC 랭킹 기록만 남긴다. 닉네임은 T9 전까지 "게스트"로 둔다.
     - 개발자 모드: 지금처럼 코인을 적립하지 않는다.
   - 얼굴 뱃지(`IdentityChanged :444-449`)는 로그인 응답의 avatar로 그대로 붙인다.
4. **`PlayerProgressStore`의 역할을 줄인다.**
   - 남는 것: "이 PC 랭킹"(`records.json`)과 개발자 모드 통계
   - `profile.json`, `wallet.json`은 지우지 않되 개발자 모드 외에는 읽지 않는다. 계정으로 옮기지 않는다.
5. **종료 로그아웃**: `OnApplicationQuit`에서 회원이면 `/logout`을 보낸다(응답을 기다리지 않음).
6. **서버 연결 실패**: 회원 기록 저장이 실패하면 세션 메모리에 보관하고 다음 저장 때 한 번 재시도한다. 실패 사실을 결과 화면에서 읽을 수 있게 상태를 노출한다(`IHudData`).

**완료 조건과 테스트**
- 기존 화면 흐름(메뉴, 얼굴 로그인 화면)이 그대로 동작한다. 이 작업에서는 화면을 바꾸지 않는다.
- EditMode: `AccountSession` 게스트↔회원 전환
- PlayMode(`FakeAccountGateway`):
  - 게스트 판 종료 시 PC 랭킹에만 기록되고 서버 저장이 호출되지 않는다.
  - 회원 판 종료 시 `SaveRun`이 mode·coins와 함께 호출된다.
  - 게스트는 아이템이 생성되지 않고, 회원은 해금한 아이템만 생성된다.

---

### T8. 첫 화면: 로그인 선택 · 회원가입

**현재 코드**
- `GameBootstrap.Awake`가 `TitleIdle`로 전환한다(`:113`). 타이틀에서 입력이 들어오면 `OpenMainMenu`(`:313`)로 메인 메뉴를 연다.
- 화면 상태는 `MenuPage { None, Main, Setup, Profile, Shop, Ranking, Identity }`(`Presentation/UI/HUDPresenter.cs:7`)이고, `DrawMenu`(`:116-134`)가 그린다. `GameFlowState`는 바꾸지 않는다.
- 얼굴 로그인 진입: 메인 메뉴 항목(`Overlay/MainMenuScreen.cs:13-14`), 준비 화면 버튼(`SetupScreen.cs:35`). 진입 시 포즈 스타일을 UpperBody로 바꿔 웹캠을 켠다(`GameBootstrap.cs:132`).
- "게임 시작"은 PC 프로필에 이름이 없으면 닉네임 입력 화면을 띄운다(`HUDPresenter.cs:77-81`).

**변경 후 흐름**
```text
타이틀 → 로그인 선택
          ├ 얼굴 로그인 → 얼굴 촬영(T6 화면) ─────────────────────→ 메인 ("OOO님")
          ├ 회원가입 → 저장 동의 → 닉네임(중복 확인) → 몸무게·기본 방식(건너뛰기 가능)
          │          → 얼굴 촬영 → 가입 완료 + 자동 로그인 → 튜토리얼 제안(T12 이후) → 메인
          └ 게스트 플레이 ─────────────────────────────────────→ 메인 ("게스트")

메인: 게임 시작 → 준비(방식·목표) → 플레이 → 결과
메인: 랭킹 / 상점 / 내 정보(회원) / 로그아웃(회원) / 설정 / 종료
```

**변경**
1. `MenuPage`에 `Login`, `SignUp`을 추가하고 `Presentation/UI/LoginGateScreen.cs`, `Presentation/UI/SignUpScreen.cs`를 만든다.
   - 버튼은 기존 `HudSkin`/`OverlaySkin` 스타일을 쓰고, 키보드와 손 가리키기(0.9초 유지)를 모두 지원한다.
2. 타이틀 입력 후(`GameBootstrap.cs:313` 부근) 메인 대신 `Login`을 연다. 로그인 선택과 회원가입 화면에서는 포즈 스타일을 UpperBody로 둔다.
3. **회원가입**은 단계형 화면이다.
   - 동의: 저장 항목은 "얼굴 특징값, 닉네임, 설정, 플레이 기록"이고, 사진은 저장하지 않는다고 안내한다.
   - 닉네임: 입력이 끝나면 `NameCheck`로 바로 확인하고, 사용할 수 없으면 사유를 표시한다.
   - 설정: 몸무게와 기본 방식을 입력한다. 건너뛰기를 누르면 기본값(몸무게 65kg, 전신)을 쓴다.
   - 얼굴 촬영: T6 화면에서 `Enroll`을 호출한다. 실패하면 앞 단계 입력을 유지한 채 촬영만 다시 한다.
4. 기존 `IdentityScreen`은 얼굴 촬영 단계 전용으로 축소한다. 로그인과 가입이 같은 화면을 쓴다. 로그아웃·삭제 버튼은 T9의 "내 정보"로 옮긴다.
5. **메인 메뉴**
   - "얼굴 로그인" 항목을 회원일 때 "로그아웃"으로 바꾼다. 누르면 로그아웃한 뒤 `Login` 화면으로 간다.
   - 게스트일 때는 "로그인 / 회원가입"으로 바꾼다. 누르면 `Login` 화면으로 간다.
   - 상단에 "OOO님" 또는 "게스트"를 표시한다.
   - 준비 화면의 얼굴 로그인 버튼(`SetupScreen.cs:35`)을 없앤다.
6. "게임 시작" 때 닉네임을 묻는 단계(`HUDPresenter.cs:79`, 첫 프로필 화면)를 없앤다.
7. **서버 연결 실패** 시 얼굴 로그인·회원가입 버튼을 비활성화하고 사유를 표시한다. 게스트 플레이는 항상 가능하다.
8. **개발자 모드**의 "다음 플레이어"(`GameBootstrap.cs:381-388`)는 로그아웃 후 `Login` 화면으로 간다.
9. `docs/GAME_FLOW_UI.md`의 화면 흐름도와 설명을 새 흐름으로 바꾼다.

**완료 조건과 테스트**
- PlayMode(`GameSceneTests` 또는 `MergedMenuPlayModeTests`)
  - 부팅 후 타이틀 입력 시 `Login` 화면이 열린다.
  - 게스트 선택 시 메인이 열리고 "게임 시작"에서 닉네임을 묻지 않는다.
  - `FakeAccountGateway`로 가입 단계(동의 → 닉네임 → 설정 → 촬영)를 통과하면 회원이 된다.
  - 로그아웃하면 `Login` 화면으로 돌아간다.

---

### T9. 내 정보 · 회원 상점 · 게스트 닉네임

**현재 코드**
- 프로필 화면(`Presentation/UI/Overlay/ProfileScreen.cs`): PC 프로필의 이름·몸무게·기본 방식을 편집한다. 로그인 중에는 이름 칸을 잠근다(`:42-45`).
- 상점(`Presentation/UI/Overlay/ShopScreen.cs`): PC 지갑으로 해금한다. 가격은 `ItemCatalog`(`Domain/PowerUps.cs:18-23`)에 있다.
- 결과 화면(`Presentation/UI/Overlay/LeaderboardScreen.cs`, `OpenResult :34`): 기록 이름은 판이 끝날 때 정해진다(`GameBootstrap.cs:350-351`).

**변경**
1. **내 정보**(회원 전용, `ProfileScreen`을 개편)
   - 닉네임: 수정 후 저장하면 `UpdateProfile`을 호출한다. 중복이면 사유를 표시한다.
   - 몸무게·기본 방식·칼로리 목표: 저장하면 `UpdateProfile`을 호출한다.
   - 기록 요약: `Stats` 결과(누적 kcal, 모드별 최고 거리)
   - 로그아웃, 탈퇴(얼굴·기록 삭제, 두 번 확인 후 `Delete`)
   - 게스트에게는 메뉴 항목을 보이지 않는다.
2. **상점**
   - 회원: `AccountSession`의 코인과 해금 목록을 표시하고, 구매는 `Buy`로 처리한다. 성공하면 응답의 잔액과 해금 목록을 반영한다.
   - 게스트: 아이템 목록과 효과는 보여 주되 구매 버튼 대신 "회원가입하면 코인을 모아 아이템을 해금할 수 있어요"를 표시한다.
3. **게스트 닉네임**(결과 화면)
   - 게스트의 판이 끝나면 결과 화면에 "이 PC 랭킹에 남길 닉네임" 입력 칸과 [저장] [건너뛰기]를 표시한다.
   - 기록은 판 종료 시 "게스트"로 먼저 남기고, 저장을 누르면 그 기록의 이름을 바꾼다. 최대 10자다.
   - 건너뛰기는 손 가리키기로도 누를 수 있다.
   - 개발자 모드 게스트는 입력 없이 기존 "플레이어 N" 이름을 쓴다(`GameBootstrap.cs:351`).
4. `docs/GAME_FLOW_UI.md`의 "데이터 저장 범위" 표를 1장 결정에 맞게 바꾼다. `AGENTS.md`의 "PC 지갑·랭킹을 중앙 DB 데이터로 혼동하지 않는다" 문구를 새 구조(코인·해금은 회원 계정, PC에는 PC 랭킹만)로 고친다.

**완료 조건과 테스트**
- PlayMode(`FakeAccountGateway`)
  - 회원 닉네임 변경이 `AccountSession`에 반영된다.
  - 회원 구매 후 해금 목록이 갱신된다.
  - 게스트는 구매할 수 없다.
  - 게스트 닉네임 저장 후 PC 랭킹 기록 이름이 바뀐다.

---

### T10. 모드별 랭킹 · 칼로리 합계 · 내 최고 기록

**현재 코드**
- `RankingEntry`(`Domain/Ranking.cs:30-35`): `Name, Score, Distance, Coins, Style(라벨 문자열), Ticks`. 계정 id와 kcal이 없다.
- `Leaderboard`(`:37-68`): 모든 모드를 한 목록으로 다루고, 상한 500개도 전체 기준이다. 필터는 `View(todayOnly, now)`뿐이다.
- 결과/메뉴 랭킹 화면(`LeaderboardScreen.cs`): 탭은 전체/오늘(`:103-110`)뿐이다. 칼로리 패널은 이번 판과 동작별 값만 보여 준다(`:86-100`).
- `PlayerStats.TotalKcal`(`Ranking.cs:76-80`)은 PC 전체 합계다.

**결과 화면 배치**
```text
┌ 결과 ───────────────────────────────────────────────────┐
│ 2,605점  (내 최고 2,420점)            [신기록!]               │
│ 플레이 3:12 · 거리 1,240m · 코인 +34 · 최고 콤보 12            │
│ [회원 랭킹][이 PC]  [전신][상체][키보드]  [전체][오늘]          │
│  1. ...   2. ...   3. ...        ┌ 칼로리 ────────────┐    │
│  ...                             │ 이번 판   18.4kcal   │    │
│  ▶ 내 기록 7위                    │ 오늘 합계 52.1kcal   │    │
│                                  │ 누적 합계 310kcal    │    │
│ (게스트) 닉네임: [______] [저장] [건너뛰기]                   │
│ [다시]  [웹캠 보정(T11)]  [메인으로]                          │
└─────────────────────────────────────────────────────────┘
```

**변경**
1. **Domain**: `RankingEntry`에 `Mode`(int, PlayStyle 값), `AccountId`(string, 게스트는 빈 값), `Kcal`(double)을 추가한다.
   - 불러올 때 `Mode`가 없는 기록은 `Style` 라벨("전신"/"상체"/"키보드")로 채운다. 라벨이 없으면 -1(미분류)로 두고 모드 탭에서 제외한다.
2. **필터와 상한**: `Leaderboard.View(todayOnly, now, mode)`와 `RankOf(entry, sameDay, mode)`를 추가한다. 상한은 모드별 500개로 바꾼다. 기존 시그니처는 호출부를 모두 옮긴 뒤 삭제한다.
3. **랭킹 화면**(결과/메뉴 공통)에 탭 세 줄을 둔다: [회원 랭킹 | 이 PC] × [전신 | 상체 | 키보드] × [전체 | 오늘]
   - 회원 랭킹: `Leaderboard` API를 호출한다. 응답 전에는 "불러오는 중"을, 실패하면 "서버에 연결할 수 없어요"를 표시한다.
   - 이 PC: 로컬 `Leaderboard`. 회원 기록에는 회원 표시를 붙인다.
   - 결과 화면 기본값: 회원이면 회원 랭킹, 게스트면 이 PC. 모드는 방금 플레이한 모드, 기간은 오늘
   - 탭과 버튼은 손 가리키기로도 선택할 수 있다.
4. **칼로리 패널**
   - 회원: 기록 저장 뒤 `Stats`를 받아 이번 판 / 오늘 합계 / 누적 합계를 표시한다.
   - 게스트: 이번 판만 표시하고 "회원가입하면 누적 칼로리를 볼 수 있어요"를 안내한다.
   - 키보드 판은 이번 판을 계산하지 않는다는 기존 문구를 유지하되, 회원의 합계 줄은 표시한다.
5. **내 최고 기록**(회원)
   - 결과 화면 제목 줄에 이번 판 점수와 함께 "내 최고 N점"을 표시한다.
   - 값은 `Stats`의 모드별 최고 기록(4.2)을 쓴다. 이번 판 저장 **전** 값과 비교해서, 넘었으면 "신기록!"을 표시한다.
   - HUD의 "최고 기록"(`IHudData.BestDistance`, 현재 PC 전체 최고 거리)도 회원의 현재 모드 최고 기록으로 바꾼다.
   - 게스트: 최고 기록을 표시하지 않고 "회원가입하면 내 최고 기록을 볼 수 있어요"를 안내한다.
6. **랭킹 정렬**: 모든 모드가 점수 기준이고, 점수 버전 2 기록만 넣는다(T13).
7. 프로필 화면에서 쓰던 `PlayerStats`는 개발자 모드 통계로만 남긴다.

**완료 조건과 테스트**
- EditMode: 모드 필터, 모드별 순위, 모드별 상한, `Style`만 있는 예전 기록 변환
- PlayMode(`FakeAccountGateway`): 회원 결과 화면 기본 탭이 회원 랭킹·현재 모드·오늘이다. 게스트는 이 PC 탭이다.
- 칼로리 패널이 회원/게스트에 따라 다르게 표시된다.
- 이전 최고 기록보다 높으면 "신기록!"이 표시되고, 낮으면 표시되지 않는다.

---

### T11. 결과 화면 웹캠 보정

**현재 코드**
- 보정: `PoseTracker.Recalibrate()`(`Infrastructure/Pose/PoseInput.cs:146`). UI에서는 `IHudData.Recalibrate()`(`HudData.cs:38`, 구현 `GameBootstrap.cs:442`)로 부른다.
- 진행률 표시: `PosePreview.Draw(..., showStatus: true)`의 보정 막대(`HudData.cs:94-99`)
- 판 시작 시 항상 다시 보정한다(`GameBootstrap.cs:204` `PrepareRun`). 그래서 결과 화면에서 보정해도 "다시"를 누르면 덮어써진다.

**변경**
1. 결과 화면 버튼 줄에 "웹캠 보정"을 추가한다. 카메라 모드에서만 표시한다.
2. 누르면 결과 화면 위에 보정 패널을 띄운다.
   - 큰 `PosePreview`(`showStatus: true`), "정면에서 가만히 서 주세요", [닫기]
   - 완료되면 "보정 완료"를 1초 표시하고 닫는다.
3. `PoseTracker`에 마지막 보정 완료 시각을 기록한다. `PrepareRun`은 보정이 60초 이내에 끝났고 그 뒤 인식이 끊기지 않았으면 `Recalibrate()`를 건너뛴다.
4. 보정 패널이 열려 있는 동안 손 가리키기로 메뉴 버튼이 눌리지 않게 막는다. 닫기는 키보드(Esc)나 보정 완료로만 닫힌다.

**완료 조건과 테스트**
- EditMode: 보정 직후 `PrepareRun` 경로에서 재보정을 건너뛰고, 60초가 지났거나 인식이 끊긴 뒤에는 재보정한다(판단 로직을 테스트 가능한 메서드로 분리).
- PlayMode: 키보드 모드에서는 버튼이 보이지 않는다.

---

### T12. 튜토리얼

**제약**: GameScene 하나만 쓰고 scene reload를 하지 않는다. 튜토리얼은 같은 씬에서 실행하는 모드다.

**재사용할 코드**
- 맵: `ChunkSpawner.Reset(seed)`/`Tick`(`Presentation/Map/ChunkSpawner.cs:79-130`)
  - 청크 종류는 `RouteSurface.UsePlan(new RoutePlan(...))`(`GameBootstrap.cs:206`, `Domain/RoutePlan.cs`)로 정한다.
  - 장애물은 `chunk.Prepare(seq, chance, random)`(`ChunkSpawner.cs:116`)로 배치한다.
- 안내: `ActionCuePresenter.Find`(`Presentation/UI/ActionCuePresenter.cs:10-54`)가 가까운 장애물을 동작 안내(점프·숙이기·하이니·옆걸음·레일 기울이기)로 바꾼다.
- 진입·초기화: `PrepareRun`(`GameBootstrap.cs:199-214`), `Retry`(`:364-373`)
- 통과 판정: `ObstacleController.Passed`(`Presentation/Map/ObstacleController.cs:11`)

**변경**
1. **`Domain/TutorialScript.cs`**(UnityEngine 미참조)
   - 플레이 방식별 단계 목록: 보정 → 좌우 이동 → 점프 → 숙이기 → 하이니 → 갈림길 기울이기 → 완료
   - 해당 방식에 없는 동작은 뺀다.
   - 각 단계가 기다리는 `MotionCommand`, 통과 조건, 현재 단계, 재시도 횟수를 가진다.
2. **`RunSession.IsTutorial`** 플래그를 추가한다. 새 `GameFlowState`는 만들지 않는다.
   - 튜토리얼 중 `Fail()`은 Caught로 가지 않고 "다시 해 볼까요?"를 표시한 뒤 그 단계 앞에서 다시 시작한다.
3. **맵**: 고정 seed와 튜토리얼 전용 `RoutePlan`을 쓴다. 단계마다 해당 동작이 필요한 장애물을 하나씩, 확률 대신 지정 목록으로 배치한다.
4. **`Presentation/Flow/TutorialDirector.cs`**
   - 장애물에 다가가면 속도를 줄이고 안내를 크게 띄운다.
   - 맞는 동작을 하면 다시 진행하고, `Passed`로 단계 완료를 판정한다.
5. **진입**
   - 메인 메뉴 "튜토리얼"
   - 회원가입 직후 제안
   - `TutorialDone`이 false인 회원과 모든 게스트가 "게임 시작"을 누르면 한 번 제안
6. **완료 처리**: 회원이면 `UpdateProfile(tutorial_done=true)`. 튜토리얼 기록은 랭킹·코인·칼로리에 넣지 않는다. 아이템은 등장시키지 않는다.

**완료 조건과 테스트**
- EditMode: 방식별 단계 목록, 단계 전이, 실패 후 재시작
- PlayMode: 튜토리얼 판 종료 시 랭킹·코인이 바뀌지 않는다. 키보드 방식으로 끝까지 진행된다.

---

## 4. 서버 명세 (T4, T5 구현 / T7 이후 사용)

### 4.1 스키마 v2

```sql
-- PostgreSQL 기준. SQLite는 BYTEA→BLOB, BOOLEAN→INTEGER, JSONB→TEXT
CREATE TABLE schema_version (version INTEGER NOT NULL);           -- 값 2
CREATE TABLE profiles (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    name_key TEXT NOT NULL UNIQUE,          -- 닉네임 정규화 값(4.3)
    weight DOUBLE PRECISION NOT NULL DEFAULT 65,  -- 미입력 시 65kg
    style INTEGER NOT NULL DEFAULT 0,       -- 0 전신, 1 상체, 2 키보드 (PlayStyle)
    goal DOUBLE PRECISION NOT NULL DEFAULT 50,
    coins INTEGER NOT NULL DEFAULT 0 CHECK (coins >= 0),
    unlocked JSONB NOT NULL DEFAULT '[]',   -- ItemKind 정수 배열
    tutorial_done BOOLEAN NOT NULL DEFAULT FALSE,
    created_at BIGINT NOT NULL,
    consent_version TEXT NOT NULL           -- 새 가입은 'account-v2'
);
-- face_samples, sessions: 변경 없음
CREATE TABLE runs (
    -- 기존 열 유지: id, profile_id, distance, score, kcal, goal, activities, created_at
    mode INTEGER,                           -- 기존 기록은 NULL
    coins INTEGER NOT NULL DEFAULT 0,
    score_version INTEGER NOT NULL DEFAULT 1,  -- 새 점수 공식(T13) = 2, 기존 기록 = 1
    play_seconds DOUBLE PRECISION NOT NULL DEFAULT 0
);
CREATE INDEX runs_mode_score ON runs (mode, score DESC);
```

### 4.2 API

모든 요청은 JSON POST다(`GET /health` 제외). 인증이 필요한 경로는 `Authorization: Bearer <token>` 헤더를 쓴다. 오류 응답은 지금처럼 `{ok:false, message}`이고, 상태 코드는 400/401/404/409/429다.

**프로필 객체** (응답의 `profile` 필드)
```json
{"id":"…","name":"민수","weight":65,"style":0,"goal":50,"coins":120,"unlocked":[0,2],"tutorial_done":false}
```

| 경로 | 인증 | 요청 | 응답 |
| --- | --- | --- | --- |
| `/name-check` | - | `{name}` | `{ok, available, message}` |
| `/enroll` | - | `{name, consent:true, images:[3], weight, style, goal}` | `{ok, token, avatar, profile}`. 닉네임 중복 409, 이미 등록된 얼굴 409 |
| `/login` | - | `{images:[3]}` | `{ok, token, avatar, profile}` |
| `/profile` | 필요 | `{}` | `{ok, profile}` |
| `/profile/update` | 필요 | `{name?, weight?, style?, goal?, tutorial_done?}` (보낸 필드만 변경) | `{ok, profile}`. 닉네임 중복 409 |
| `/runs` | 필요 | `{id(uuid), distance, score, kcal, goal, activities:[5], mode, coins, play_seconds, score_version}` | `{ok, coins}`(적립 후 잔액). 같은 id 재전송은 적립 없이 현재 잔액 |
| `/shop/buy` | 필요 | `{item}` | `{ok, profile}`. 코인 부족 400, 이미 해금 409, 없는 아이템 400 |
| `/leaderboard` | 선택 | `{mode, today}` | `{ok, entries:[{rank, name, score, distance, created_at}], me:{rank, score, distance}}`. `me`는 토큰이 있고 기록이 있을 때만 |
| `/stats` | 필요 | `{}` | `{ok, today_kcal, total_kcal, best_score:[전신, 상체, 키보드]}` |
| `/logout` | 필요 | `{}` | `{ok}` |
| `/delete` | 필요 | `{}` | `{ok}`. 프로필·특징값·세션·기록 삭제 |
| `/history` | 필요 | 기존 유지 | 기존 유지 |

- `avatar`: 이번 요청 첫 장의 정렬 얼굴 JPG(base64). DB에 저장하지 않는다.
- `/leaderboard`
  - 정렬 기준: 모든 모드가 `score`이고 `score_version=2` 기록만 넣는다.
  - 해당 모드 회원 기록에서 사람당 최고 기록 1개로 상위 50명을 만든다.
  - 동점이면 먼저 세운 기록이 앞선다.
  - `today`는 KST 0시 이후 기록만 본다.
  - `name`은 현재 닉네임이다.
- `/runs` 검증
  - 기존 숫자 검사(`server.py:78-85`)를 유지한다.
  - `mode`는 0~2, `coins`는 0~5000 정수, `score_version`은 1 또는 2, `play_seconds`는 0~86400이다.
- `/stats`의 `best_score`는 `score_version=2` 기록만 본다. 기록이 없으면 0이다.
  - 코인 적립과 기록 삽입은 한 트랜잭션이다.

### 4.3 공통 값

| 항목 | 값 |
| --- | --- |
| 닉네임 규칙 | 앞뒤 공백 제거 후 1~10자. 제어문자 금지 |
| 닉네임 정규화(`name_key`) | 공백 제거 + 소문자(`casefold`) |
| 아이템 | 0 자석 30코인, 1 코인 2배(구 2배 점수) 60코인, 2 방패 100코인. 클라이언트 `ItemCatalog`(`Domain/PowerUps.cs:18-23`)와 같은 값 |
| 플레이 방식 | 0 전신, 1 상체, 2 키보드(`PlayStyle`) |
| 세션 | 8시간(기존 유지). 클라이언트는 종료 시 `/logout` |
| 시도 제한 | `/enroll`·`/login` 합산 1분 15회(상수화), `/name-check` 1분 60회 |
| 코인 신뢰 | 코인 값은 클라이언트가 보낸다. 전시용 허용 범위이며 README에 명시한다 |

---

## 5. 남은 확인 사항

1. **숙이기 기준값 (T2)**: 0.40 / 0.20초는 출발값이다. 실제 웹캠 실측으로 확정한다.
2. **해금 가격 (T3)**: 한 판에 얻는 코인 수를 실측한 뒤 30/60/100을 유지할지 정한다. 바꾸면 클라이언트 `ItemCatalog`와 서버 4.3을 함께 바꾼다.
3. **점수에 쓰는 칼로리 (T13)**: 점수의 칼로리는 공정성을 위해 입력 몸무게가 아니라 65kg 기준으로 계산한다(화면에 보이는 칼로리는 입력 몸무게 기준). 입력 몸무게 기준으로 바꾸려면 T13 시작 전에 정한다.
   - 칼로리 × 100의 비중: 전신 모드 1분 운동 ≈ 455점, 상체 ≈ 284점이다. 거리(1m = 1점)와의 균형은 플레이 실측 후 계수(100)를 조정할 수 있다.
4. **중앙 서버 적용 (T4, T5)**: 기존 사진 삭제와 스키마 마이그레이션은 되돌릴 수 없다. 실행 전 확인을 받고 DB 백업(`README_DOCKER.md` 절차)을 먼저 한다.
