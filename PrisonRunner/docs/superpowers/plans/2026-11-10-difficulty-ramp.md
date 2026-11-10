# 난이도 완만화 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 거리 기반 진행도(0~1) 하나로 속도와 장애물 확률을 "쉬운 시작값 → 현재 최대값"으로 천천히 올린다.

**Architecture:** 순수 Domain `DifficultyDirector`가 진행도와 `HazardOdds`(줄·차단물·2레인·바나나 확률)를 계산한다. `RunSession`은 확장 경로 속도를, `ObstacleLayout`은 하드코딩 확률 대신 `HazardOdds`를 쓴다. Presentation은 청크 시작 거리로 `HazardOdds`를 구해서 넘기기만 한다.

**Tech Stack:** Unity 6000.3.10f1, C# (Domain은 UnityEngine 없음), NUnit EditMode.

**Spec:** `PrisonRunner/docs/superpowers/specs/2026-11-10-difficulty-ramp-design.md`

## Global Constraints

- Domain/Core는 UnityEngine을 참조하지 않는다.
- 패키지·Unity 버전을 바꾸지 않는다. 씬을 재생성하지 않는다.
- 커밋 메시지에 작성 도구/생성 주체 문구와 Co-Authored-By를 넣지 않는다. 커밋 하나에 목적 하나.
- 같은 seed는 같은 배치를 낸다. `ObstacleLayout.Plan`의 `random` 호출 순서와 횟수를 바꾸지 않는다(비교 대상 확률만 바꾼다).
- 출시 값: 걷기 6→12, 광차 5→7, 줄 .25→.9, 차단물 .25→.72, 2레인 .3→.85, 바나나 .3→.82, 유예 150m, 최대 도달 2000m, smoothstep.
- 동물원 차단물 확률 = 차단물 확률 × (.52/.72).

## 검증 명령

worktree에는 Unity Library가 없으므로 아래 스크립트로 컴파일과 순수 규칙 테스트를 돌린다. 이 스크립트는 원래 체크아웃의 Library를 읽기만 한다.

```powershell
& "C:\Users\rhkdr\AppData\Local\Temp\claude\1befa173d4532b6f\8b9447c4-d966-41eb-9739-0d41b9836f1a\scratchpad\wt_rules.ps1" -Tests RunRulesTests,ExpansionRulesTests,BodyTrackingAndExhibitionTests,ObstacleLayoutTests
```

기준선: `86 passed, 1 failed`. 실패는 `TrackerSkipsRecalibrationRightAfterCalibratingAndNotAfterLosingThePlayer`(러너 환경의 MissingMethodException)이며 이번 작업과 무관하다. "통과"는 이 1건 외에 FAIL이 없다는 뜻이다.

## Review Focus

1. `DifficultyFullDistance <= DifficultyGraceDistance`로 잘못 설정한 경우: 0으로 나누지 말고, full 이상이면 1, 미만이면 0으로 처리한다. → Task 1 테스트.
2. 음수 거리(시작 직후 z 2 이전 또는 sequence 0): 진행도 0. → Task 1 테스트.
3. 진행도 1 이후 아주 먼 거리(예: 100000m): 최대값에서 멈추고 넘지 않는다. → Task 1·2 테스트.
4. seed 결정성: 같은 seed·같은 odds면 같은 배치. 기존 `SameSeedGivesTheSameLayoutAndSeedsVaryIt`가 최대 odds 기준으로 그대로 통과해야 한다. → Task 3.
5. 시작 odds에서도 안전 규칙: 모든 청크 타입·여러 seed에서 `Violation()==null`. → Task 3 테스트.

---

### Task 1: 진행도와 HazardOdds

**Files:**
- Modify: `PrisonRunner/Unity/Assets/Game/Domain/RunRules.cs` (`RunConfig`, `DifficultyDirector`)
- Test: `PrisonRunner/Unity/Assets/Game/Tests/EditMode/RunRulesTests.cs`

**Interfaces:**
- Produces:
  - `public readonly struct HazardOdds { public readonly float Row, Blocker, DoubleBlocker, Banana; public HazardOdds(float row, float blocker, float doubleBlocker, float banana); }` (`RunRules.cs`, namespace `Muhanok.Domain`)
  - `RunConfig` 신규 필드와 기본값: `MaxRestSpeed=7f`, `DifficultyGraceDistance=150f`, `DifficultyFullDistance=2000f`, `ObstacleChanceStart=.25f`, `ObstacleChanceMax=.9f`, `BlockerChanceStart=.25f`, `BlockerChanceMax=.72f`, `DoubleBlockerStart=.3f`, `DoubleBlockerMax=.85f`, `BananaChanceStart=.3f`, `BananaChanceMax=.82f`
  - `DifficultyDirector.Progress(float distance, RunConfig config) -> float` (smoothstep 0~1)
  - `DifficultyDirector.Ramp(float start, float max, float distance, RunConfig config) -> float` (= start + (max-start)×Progress)
  - `DifficultyDirector.Odds(float distance, RunConfig config) -> HazardOdds`
  - 기존 `ObstacleChance(float)`는 삭제한다(호출처는 Task 4에서 교체).

- [ ] **Step 1: 실패하는 테스트 작성** (`RunRulesTests`)

```csharp
[TestCase(-50f,0f)] [TestCase(0f,0f)] [TestCase(150f,0f)] [TestCase(1075f,.5f)] [TestCase(2000f,1f)] [TestCase(100000f,1f)]
public void DifficultyProgressWaitsThenEasesToFull(float distance,float expected)
{ Assert.AreEqual(expected,new DifficultyDirector().Progress(distance,new RunConfig()),.001f); }

[Test] public void DifficultyProgressRisesMonotonically()
{ var d=new DifficultyDirector();var c=new RunConfig();float last=0;
  for(float m=0;m<=2500;m+=25){float p=d.Progress(m,c);Assert.GreaterOrEqual(p,last);last=p;} }

[Test] public void DifficultyProgressHandlesAFullDistanceAtOrBeforeTheGrace()
{ var d=new DifficultyDirector();var c=new RunConfig{DifficultyGraceDistance=300,DifficultyFullDistance=300};
  Assert.AreEqual(0,d.Progress(299,c));Assert.AreEqual(1,d.Progress(300,c)); }

[Test] public void HazardOddsGoFromStartToMax()
{ var d=new DifficultyDirector();var c=new RunConfig();
  var s=d.Odds(0,c);Assert.AreEqual(.25f,s.Row,.001f);Assert.AreEqual(.25f,s.Blocker,.001f);Assert.AreEqual(.3f,s.DoubleBlocker,.001f);Assert.AreEqual(.3f,s.Banana,.001f);
  var m=d.Odds(5000,c);Assert.AreEqual(.9f,m.Row,.001f);Assert.AreEqual(.72f,m.Blocker,.001f);Assert.AreEqual(.85f,m.DoubleBlocker,.001f);Assert.AreEqual(.82f,m.Banana,.001f); }
```

- [ ] **Step 2: 검증 명령 실행 → EditModeTests 컴파일 실패(`Progress`/`Odds`/`HazardOdds` 없음) 확인**
- [ ] **Step 3: `RunRules.cs`에 위 Interfaces 구현.** 진행도: `distance >= full`이면 1, `distance <= grace`면 0, 그 외 `t=(d-grace)/(full-grace)`, `t*t*(3-2t)`. full≤grace일 때 앞의 두 조건만으로 처리되도록 비교 순서를 지킨다.
- [ ] **Step 4: 검증 명령 실행 → 신규 테스트 PASS, 기준선 외 FAIL 없음**
- [ ] **Step 5: 커밋** `feat(difficulty): add a distance progress curve and hazard odds`

### Task 2: 확장 경로 속도를 진행도로

**Files:**
- Modify: `PrisonRunner/Unity/Assets/Game/Domain/RunRules.cs` (`RunConfig`, `RunSession.IntervalSpeed`)
- Test: `PrisonRunner/Unity/Assets/Game/Tests/EditMode/ExpansionRulesTests.cs` (`ExtendedChallengeBuildsWithinALapAndKeepsRestAndPauseRules`)

**Interfaces:**
- Consumes: `DifficultyDirector.Ramp` (Task 1)
- Produces: `RunConfig.ExtendedSpeedRampDistance`/`ExtendedSpeedStep` 삭제.

- [ ] **Step 1: 기존 테스트를 새 규칙으로 바꾼다** (config: `IntervalMode=true, ExtendedRoutes=true, SprintSpeed=6, MaxSprintSpeed=12, RestSpeed=5, MaxRestSpeed=7`)
  - 0m: `TargetSpeed == 6`
  - `Score.Advance(1075)`: `TargetSpeed == 9` (±.001)
  - `Score.Advance(10000)` 추가: `TargetSpeed == 12`
  - `Runner.SetStartZ(30)`(광차): `TargetSpeed == 7` (거리가 최대이므로), 메시지 "The cart rest speed ramps with the run"
  - 신규 assert: 거리 0인 새 `RunSession`에서 `SetStartZ(30)` → `TargetSpeed == 5`
  - 신규 assert, 바퀴 경계에 계단 없음: `RoutePlan.ExtendedLapChunks*RouteSurface.Length`를 앞뒤로 1m씩 걷는 구간에서(`SetStartZ`로 위치 지정, 같은 Score 거리) `TargetSpeed` 차이 < .01
  - 일시정지 assert는 유지한다.
- [ ] **Step 2: 검증 명령 → `ExtendedChallengeBuildsWithinALapAndKeepsRestAndPauseRules`가 값 불일치로 FAIL 확인**
- [ ] **Step 3: `IntervalSpeed()` 수정.** `Config.ExtendedRoutes`이면 탄 상태 `Ramp(RestSpeed, MaxRestSpeed)`, 걷기 `Ramp(SprintSpeed, MaxSprintSpeed)`(둘 다 `Score.Distance` 기준, `SprintSpeedStep×IntervalCycle` 없음). ExtendedRoutes=false면 지금 코드(`RestSpeed`, `min(MaxSprintSpeed, SprintSpeed+SprintSpeedStep×IntervalCycle)`) 그대로 둔다. `ExtendedSpeedRampDistance`/`ExtendedSpeedStep` 필드를 삭제한다.
- [ ] **Step 4: 검증 명령 → PASS. `IntervalSpeedRestsInTheCartAndRisesEachSprint`(비확장 경로)도 그대로 PASS**
- [ ] **Step 5: 커밋** `feat(difficulty): ramp walking and cart speed along the progress curve`

### Task 3: ObstacleLayout이 HazardOdds를 받도록

**Files:**
- Modify: `PrisonRunner/Unity/Assets/Game/Domain/ObstacleLayout.cs` (`Plan`)
- Test: `PrisonRunner/Unity/Assets/Game/Tests/EditMode/ObstacleLayoutTests.cs`

**Interfaces:**
- Consumes: `HazardOdds` (Task 1)
- Produces: `ObstacleLayout.Plan(Random random, int type, int sequence, IReadOnlyList<ObstacleSlot> authored, IReadOnlyList<LanePlacement> floorItems, HazardOdds odds, bool extended, bool fork, bool boarding = false)`; `public const float ZooBlockerScale = .52f / .72f;`

- [ ] **Step 1: 테스트 수정·추가**
  - 테스트 헬퍼 `private static HazardOdds Odds(float row) => new HazardOdds(row, .72f, .85f, .82f);`를 추가하고, 기존 `chance` 인자 자리(헬퍼 `Plan`, 139·149·154·158행)를 `Odds(chance)`로 바꾼다. 기존 테스트는 같은 결과로 통과해야 한다.
  - 신규 `StartOddsPlaceFewerBlockersAndBananasThanMaxOdds`: 타입 0, sequence 1, seed 0~199, extended=true. 시작 odds `new HazardOdds(.25f,.25f,.3f,.3f)`의 `Blockers.Count+Bananas.Count` 합이 최대 odds `Odds(.9f)` 합의 60% 미만이다.
  - 신규 `StartOddsKeepTheSafetyRules`: 시작 odds로 `EverySeedAndChunkTypeKeepsTheSafetyRules`와 같은 타입·seed 범위를 돌려 `Violation()`이 모두 null이다(그 테스트의 루프를 odds 인자로 공유해도 된다).
- [ ] **Step 2: 검증 명령 → 컴파일 실패 확인**
- [ ] **Step 3: `Plan` 시그니처를 바꾸고 비교값을 교체한다.** `(type == 12 ? .52 : .72)` → `type == 12 ? odds.Blocker * ZooBlockerScale : odds.Blocker`, `.85` → `odds.DoubleBlocker`, `.82` → `odds.Banana`, `chance` → `odds.Row`. random 호출 순서는 그대로.
- [ ] **Step 4: 검증 명령 → PASS**
- [ ] **Step 5: 커밋** `feat(difficulty): take lane blocker and banana odds from the difficulty curve`

### Task 4: 청크 생성 연결과 출시 값

**Files:**
- Modify: `PrisonRunner/Unity/Assets/Game/Presentation/Map/MuhanokMapChunk.cs:88-95` (`Prepare`)
- Modify: `PrisonRunner/Unity/Assets/Game/Presentation/Map/ChunkSpawner.cs:118`
- Modify: `PrisonRunner/Unity/Assets/Game/Content/Data/GameSettings.asset` (`Run:` 블록)
- 그 외 `Prepare(` 호출처가 있으면 같이 고친다(`grep -rn "\.Prepare(" Unity/Assets/Game`).

**Interfaces:**
- Consumes: `DifficultyDirector.Odds`, `ObstacleLayout.Plan(..., HazardOdds, ...)`
- Produces: `MuhanokMapChunk.Prepare(int sequence, HazardOdds odds, System.Random random)`

- [ ] **Step 1: `Prepare`가 `HazardOdds`를 받아 `Layout.Plan`에 넘기도록 수정**
- [ ] **Step 2: ChunkSpawner 호출을 `session.Difficulty.Odds(sequence * RouteSurface.Length, session.Config)`로 교체** (현재 플레이어 거리가 아니라 청크 시작 거리)
- [ ] **Step 3: `GameSettings.asset` `Run:` 수정.** `SprintSpeed: 6`, `RestSpeed: 5`, `MaxSprintSpeed: 12` 유지, `ExtendedSpeedRampDistance`/`ExtendedSpeedStep` 줄 삭제, 아래 줄 추가(같은 들여쓰기 4칸):
  `MaxRestSpeed: 7`, `DifficultyGraceDistance: 150`, `DifficultyFullDistance: 2000`, `ObstacleChanceStart: 0.25`, `ObstacleChanceMax: 0.9`, `BlockerChanceStart: 0.25`, `BlockerChanceMax: 0.72`, `DoubleBlockerStart: 0.3`, `DoubleBlockerMax: 0.85`, `BananaChanceStart: 0.3`, `BananaChanceMax: 0.82`
- [ ] **Step 4: 검증 명령 → 모든 어셈블리 PASS(Presentation·Bootstrap·PlayModeTests 제외 없이 컴파일), 규칙 테스트 기준선 외 FAIL 없음.** `grep -rn "ObstacleChance(\|ExtendedSpeed" Unity/Assets/Game`이 비어 있다.
- [ ] **Step 5: 커밋** `feat(difficulty): spawn chunks with odds for their own distance and ship the easier start`

### Task 5: 문서와 플레이 확인

- [ ] **Step 1:** `PrisonRunner/docs/PROGRESS.md`에 난이도 곡선 요약(시작→최대 표, 150m/2000m, 튜닝 위치 `GameSettings.asset` Run)을 한 단락 추가한다. `DEVELOPMENT_PLAN.md` 3번 항목은 "곡선 적용, 실플레이 재조정 남음"으로 갱신한다.
- [ ] **Step 2: 커밋** `docs(difficulty): note the difficulty curve and where to tune it`
- [ ] **Step 3: 사용자 플레이 확인 요청.** Unity에서 worktree를 열거나 브랜치를 체크아웃하면 첫 임포트에 시간이 걸린다고 안내한다. 확인 항목: 첫 1분 동안 차단물·바나나가 드물다, 속도가 바퀴 경계에서 튀지 않는다, 3~4분쯤 기존 체감 난이도에 도달한다.
