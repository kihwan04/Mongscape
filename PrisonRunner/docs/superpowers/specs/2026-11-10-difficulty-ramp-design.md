# 난이도 완만화 설계 (2026-11-10)

## 목표

처음 하는 사람이 시작 직후 잡히지 않게 초기 난이도를 낮추고, 달린 거리에 따라 천천히 지금 수준까지 올린다.
수치는 테스트 플레이 후 재조정하므로 모든 값을 `GameSettings.asset`의 `RunConfig`에서 바꿀 수 있게 한다.

## 현재 문제 (develop `ee24059`, 출시 설정 IntervalMode=1, ExtendedRoutes=1)

- 걷기 속도 `min(12, 8 + 0.75×바퀴 + 거리/160×0.6)`: 시작이 8 m/s, 약 1070m(약 100초)에서 최대.
- 줄마다 장애물 확률 `min(0.9, 0.55 + 거리/2000)`: 시작부터 55%.
- 레인 차단물 72%(동물원 52%), 그중 2레인 85%, 바나나 82%: `ObstacleLayout`에 하드코딩, 거리와 무관.
- 광차 속도 6 m/s 고정.

## 설계

### 난이도 진행도

`DifficultyDirector.Progress(distance, config)`가 0~1을 돌려준다.

- `distance <= DifficultyGraceDistance`(150m)이면 0.
- `distance >= DifficultyFullDistance`(2000m)이면 1.
- 그 사이는 smoothstep(`t*t*(3-2t)`). 초반 상승이 완만하고 최대 근처에서 부드럽게 멈춘다.

### 진행도로 보간하는 값 (시작 → 최대)

| 값 | RunConfig 필드 | 시작 | 최대 |
|---|---|---|---|
| 걷기 속도 | `SprintSpeed` → `MaxSprintSpeed` | 6 | 12 |
| 광차 속도 | `RestSpeed` → `MaxRestSpeed`(신규) | 5 | 7 |
| 줄마다 장애물 | `ObstacleChanceStart` → `ObstacleChanceMax` | .25 | .9 |
| 레인 차단물 청크 | `BlockerChanceStart` → `BlockerChanceMax` | .25 | .72 |
| 그중 2레인 | `DoubleBlockerStart` → `DoubleBlockerMax` | .3 | .85 |
| 바나나 레인 | `BananaChanceStart` → `BananaChanceMax` | .3 | .82 |

- 동물원 청크의 차단물 확률은 지금 비율을 유지한다(차단물 확률 × .52/.72).
- 전신 동작 줄 `ExerciseChance`(.5)는 운동량이라 그대로 둔다.

### 코드 변경

- `Domain/RunRules.cs`
  - `RunConfig`에 위 신규 필드를 추가한다. ExtendedRoutes 경로의 `ExtendedSpeedRampDistance`/`ExtendedSpeedStep`은 진행도 곡선으로 대체되므로 삭제한다.
  - `DifficultyDirector`에 `Progress`, `Odds(distance, config)`(신규 `HazardOdds` 구조체: Row, Blocker, DoubleBlocker, Banana)를 추가한다. 기존 `ObstacleChance(distance)`는 `Odds`로 대체한다.
  - `RunSession.IntervalSpeed()`: ExtendedRoutes면 걷기·광차 속도를 진행도로 보간한다. 바퀴마다 붙던 계단식 +0.75는 이 경로에서 없앤다. ExtendedRoutes=false인 기존 인터벌 경로(`SprintSpeedStep × 바퀴`)와 비인터벌 `Speed()`는 그대로 둔다.
- `Domain/ObstacleLayout.cs`: `Plan`의 `float chance`를 `HazardOdds`로 바꾸고, 하드코딩 확률 .72/.52/.85/.82를 그 값으로 대체한다.
- `Presentation/Map/MuhanokMapChunk.cs` `Prepare`, `Presentation/Map/ChunkSpawner.cs`: `HazardOdds`를 전달한다. 기준 거리는 플레이어의 현재 거리가 아니라 **그 청크의 시작 거리**(`sequence × RouteSurface.Length`)로 한다. 그래야 미리 생성되는 청크도 위치에 맞는 난이도를 받고, 같은 seed면 같은 배치가 나온다.
- `Content/Data/GameSettings.asset`: 출시 값을 위 표대로 갱신한다.

### 유지하는 규칙

튜토리얼 흐름, 첫 줄 고정 위치(z 19), 안전 레인·`Violation` 규칙, 1회 충돌 시 잡힘, 장애물 간격(4m/6m), 최대 속도 12.

## 테스트

EditMode(순수 규칙, `Tools/QA` RuleRunner로도 실행).

- `Progress`: 150m 이하 0, 2000m 이상 1, 그 사이 단조 증가.
- 확장 경로 속도: 0m 6, 2000m 이상 12. 광차 0m 5, 최대 7. 바퀴 경계에서 계단이 없다.
- `Odds`: 0m는 시작값, 2000m는 최대값.
- `ObstacleLayout`: 시작 확률로 여러 seed를 돌리면 최대 확률보다 차단물·바나나가 적다. 양 끝 확률 모두에서 `Violation()`이 null이다.
- 기존 테스트 수정: `ExtendedChallengeBuildsWithinALapAndKeepsRestAndPauseRules`(160m 8.6 기대값 → 곡선 기준), `ObstacleLayoutTests`의 `chance` 인자 → `HazardOdds`.

## 범위 밖

플레이 방식별 난이도, 난이도 선택 UI, 자동 조절, 최대 속도 상향과 속도 비례 장애물 간격.
