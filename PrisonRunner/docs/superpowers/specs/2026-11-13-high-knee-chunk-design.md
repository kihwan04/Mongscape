# 하이니 계단 청크 설계

기준일: 2026-11-13. 하이니(무릎 들기)를 감옥 입구 계단에서만 쓰던 구조를, 모든 맵에 나오는 "계단 청크"와 좌우 번갈아 들기 판정(`KneeStep`)으로 바꾼다.

## 배경

- 확장 경로에서 계단(타입 7)은 감옥 갈래에만 바퀴당 1칸이고, 갈래는 숨겨진 셔플이라 평균 3바퀴(약 3.5km)에 1번, 하이니 줄 3개만 나왔다.
- 하이니 판정은 한쪽 무릎을 계속 들고 있거나(전신) W를 누르고 있으면(키보드) 유지돼서, 길게 이어지는 구간에서는 운동이 되지 않는다.
- 바나나 감속 2회면 추격·체포되는데, 횟수가 판 끝까지 줄지 않고 1회 뒤 추격 압력도 회복되지 않는다.

## 확정 값

| 항목 | 값 |
|---|---|
| 한 바퀴 | 48칸 → 32칸. 공통 시작 7칸 + 갈래 25칸. 감옥 갈래: 계단 1 + 복도 22 + 검문소 1(30) + 감옥→광산 출구 15(31). 사육장 없음, 높이 유지 |
| 감속 경고 | 바나나·하이니 실패를 합산(`SlipStrikes`). 1회 뒤 `StrikeClearSeconds`(8초) 동안 추가 경고가 없으면 0, 압력 −0.45. 8초 안 2회면 추격 후 체포 |
| 하이니 판정 | `KneeStep` = 직전과 반대쪽 무릎(상체는 큰 팔 흔들기 전환, 키보드는 W 새로 누름)이 기준을 넘은 1걸음. 걸음 간격 `KneeStepGap` 0.7초 |
| 계단 청크 판정 | 청크 로컬 z 2~22m. 걸음 간격이 0.7초를 넘으면 감속(경고 1회). 한 청크에 최대 1회 |
| 높이 | 청크 시작 높이 = 그 앞 청크 오르내림(`Rise`) 누적. 계단(7·13·14)은 설정값 하나(`StairRise`) **+4.5m**, 감옥 내리막(11) −3m. 바퀴가 끝나도 0으로 돌아가지 않는다 |
| 계단 청크 수 | 바퀴당 진행도에 따라 1 → 2 → 3개(감옥 입구 계단 포함), 서로 최소 6칸 간격 |
| 계단 종류 | 감옥 7(기존), 광산 13, 사육장 14(신규) |

## 수레·갈림길과 누적 높이

- 경로는 이미 갈림길 선택만큼 바퀴마다 x가 누적된다(`RoutePlan.CycleOffset`). y 누적은 같은 패턴이다.
- 수레 레일·갈림길(`RailForkView`)은 청크 자식의 로컬 좌표라 청크와 함께 올라간다.
- 선택 확정 전에는 갈림길 끝 너머 청크를 만들지 않는다(`ChunkSpawner`의 `PrefetchLimit`). 서로 다른 높이의 갈래가 동시에 보이지 않는다.

## 단계

모든 단계는 `feature/high-knee-chunk`에서 단계별 커밋으로 진행하고, 단계가 끝날 때마다 EditMode·PlayMode 테스트를 통과시킨다.

### 1. 32칸 바퀴 + 감속 경고 초기화
- `RoutePlan.ExtendedLapChunks` 32, `ExtendedLap()` 감옥 갈래 복도 22, 검문소(30), 출구(15, 31번째 칸).
- `RuntimeGameplayRecording`의 하드코딩 `1152`·`48`을 계산값으로.
- `BananaHits` → `SlipStrikes`, `Slip(bool)` → `Slip(SlipSource {Floor, Banana, KneeMiss})`. Floor는 경고에 들어가지 않는다.
- `RunConfig.StrikeClearSeconds = 8`. 해제 시 `Chase.Hit(-.45f)`.
- `CaptureReason.BananaPursuit` → `SlipPursuit`(정수값 유지). 결과 화면 문구 확인.
- 참조처: `PolicePursuitCue`, `GameBootstrap`(경찰 거리), `GameAudio`, `FloorItemController`, 런타임 검사 스크립트 3개. 녹화 스크립트의 두 번째 바나나 탐색은 8초 창 안으로.
- HUD: 경고 아이콘 둘레 8초 링, 해제 시 "경고 해제".

### 2. `HighKnee` → `KneeStep`
- `MotionClassifier`: 전신은 마지막으로 기준을 넘은 무릎 쪽을 기억하고 반대쪽이 넘으면 `Reading.KneeStep` 엣지. 첫 걸음은 어느 쪽이든. 공중이면 무시. 상체는 큰 팔 흔들기 전환에서 엣지. `kneeUntil`·`Reading.HighKnee`·`HighKneeHoldSeconds` 제거.
- `PoseInput`: 엣지일 때 1회 전송. `KeyboardGameInput`: W/↑ `wasPressedThisFrame`.
- `MotionCommand.HighKnee` → `KneeStep`(정수값 유지). `RunnerController`: `kneeLeft` 제거, `LastKneeStep`과 `KneeStepping`(최근 걸음이 `KneeStepGap` 안). `HighKneeSeconds` 제거, `KneeStepGap` 추가.
- 참조처: `RunnerView`, `WorkoutEstimate`, `HudData`, `GameBootstrap`, `TutorialScript`(성공 조건·안내 문구).
- 3단계 전까지 계단 줄은 `KneeStepping`으로 판정한다.

### 3. 누적 높이
- `RouteSurface.BaseHeight(sequence)`. `RoutePlan`이 높이 목록을 유지하고 `UpdateChoice` 뒤 재계산. 경로 계획이 없으면 기본 바퀴 반복 누적.
- `EntryHeight(type)` 제거. `Sample`, `MuhanokMapChunk` 배치, `MuhanokArtReview` 등 교체.
- 씬에서 높이 0에 고정된 청크 밖 오브젝트, 카메라·경찰·체포 장면 확인.

### 4. 계단 청크
- `RoutePlan`: 선택 확정 뒤 걷는 칸(0·4·5·12) 일부를 맵별 계단(13·7·14)으로. seed 고정. 진행도 기반 1~3개, 6칸 간격, 연결 직후·복귀 칸·전환 칸(3·6·7·11·15) 앞뒤 제외. 비확장 모드는 입구 계단만.
- `ObstacleLayout`: 계단 칸은 장애물·차단물·바나나·바닥 아이템·동물 없음. 무릎 높이(약 0.6m) 코인 줄.
- 7번 입구 점프 줄과 닫히는 숙이기 문 제거. `ObstacleKind.Stair`·`OBS_Stair_HighKnee` 제거(직렬화 값 확인).
- `Domain/StairJudge`: 실패 시 `Slip(KneeMiss)`. 계단 코인은 `KneeStepping`일 때만.
- 안내 "무릎 번갈아!", 계단 안 "무릎!" 게이지, 카메라 살짝 위로, `KneeStep`마다 화면 출렁임과 발소리.
- 13·14 청크는 먼저 `Shape`/`Beam` 임시 모양으로 만들고 청크 풀·`AreaAt`에 등록.
- 튜토리얼: 계단 청크를 감속 없이 통과하면 성공.

### 5. Blender 에셋
- Blender 5.3.0(`M:/Blender/blender.exe`)을 화면 없이 실행하는 생성 스크립트 `References/Blender/scripts/make_mine_stair.py`, `make_zoo_stair.py`. 5.x는 새 재질에 Principled BSDF를 자동 생성하지 않으므로 노드를 직접 만든다.
- FBX를 `Unity/Assets/External/Staging/Blender/`로 내보낸다. 24m에 3m 오름, 세 차선 폭, 디딤판 간격은 무릎 박자 기준. 그리기 호출·삼각형은 기존 감옥 계단 청크 이하.
- Unity 화면 확인 후 임시 모양을 교체한다.

### 6. 문서
- `GAME_FLOW_UI.md`, `BODY_TRACKING.md`, `TECH_STACK.md`, `START_HERE.md`, `README.md`, `PROGRESS.md`.

## 완료 기준

- [x] 고정 seed에서 광산·감옥·사육장 갈래 모두 계단 청크가 나온다
- [x] 계단 청크에 다른 장애물이 없다
- [x] 바퀴당 계단 청크 수가 진행도에 따라 1~3개이고 서로 6칸 이상 떨어진다
- [x] 한쪽 무릎만 들거나 W를 꾹 누르면 계단 청크에서 감속되고, 번갈아 들거나 연타하면 통과한다
- [x] 한 계단 청크에서 감속은 최대 1회다
- [x] 계단 코인은 `KneeStepping`일 때만 먹힌다
- [x] 계단을 오른 뒤 다음 청크(수레 포함)가 오른 높이에서 이어진다
- [x] 경고 1회 뒤 8초가 지나면 경고가 0이 되고 압력이 0.45 내려간다
- [x] 8초 안 두 번째 경고(바나나·하이니 실패)면 추격 후 체포된다
- [x] 튜토리얼 하이니 단계는 번갈아 들기로만 통과한다
- [x] 코드에 `MotionCommand.HighKnee`, `ObstacleKind.Stair`, `EntryHeight` 사용처가 남지 않는다
- [ ] EditMode·PlayMode 테스트와 런타임 검사 스크립트가 통과한다 (EditMode 264/264, PlayMode 77/77 통과. 런타임 검사 스크립트는 이번 단계에서 따로 실행하지 않아 미확인)

## 진행 결과

2026-11-13/14, `feature/high-knee-chunk`. 위 완료 기준 중 체크한 항목은 아래 커밋의 EditMode·PlayMode 테스트(또는 코드 검색)로 확인했다.

| 단계 | 커밋 |
|---|---|
| 설계 | `f1a69db` docs(stairs): design high-knee stair chunks, knee-step input and strike reset |
| 1 | `8340711` feat(run): 32-chunk laps and slip strikes that clear after 8 seconds, `bff6a86` feat(hud), `56c0935` test(run) |
| 2 | `6fbaa10` feat(input): replace held high-knee with alternating KneeStep events |
| 3 | `a6511f3` docs(stairs): fix the stair rise at 4.5 m, `a20635a` feat(route): accumulate chunk height |
| 4 | `9814cf3` feat(route): stair chunks in every biome, `a458749` feat(stairs): stair cue, knee gauge, camera pitch, tutorial |
| 5 | `4468418` Blender generators + FBX, `70c366b` rise 4.5 m, `ec88535` merge, `9af06c1` feat(art): stair assets integrated |
| 6 | 이 문서 갱신(docs(stairs)) |

- 감옥 갈래는 6칸 간격 때문에 바퀴당 계단이 최대 2개(입구 + 사육장 구간 1개)다.
- 튜토리얼의 무릎 실패는 경고(strike) 없이 튜토리얼 실수로만 센다.

## 미정

- 계단 높이: 2026-11-13 4.5m로 확정. 3m 오름으로는 디딤판이 0.1m 안팎이라 경사로처럼 보였다.
- 감옥 복도 중간 계단: 해결. 7번의 입구 장식(셔터 레일·간판)을 없애고 복도에서도 7번을 그대로 쓴다. 새 계단 타입은 없고 15번은 감옥→광산 출구 청크다(`CH_Prison_Exit`).
