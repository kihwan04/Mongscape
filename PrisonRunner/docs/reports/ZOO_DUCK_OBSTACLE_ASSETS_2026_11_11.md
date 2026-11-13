# 동물원 숙이기 장애물 에셋 조사

기준일: 2026-11-11. 조사만 했습니다. 코드·씬·에셋은 바꾸지 않았고, 아무것도 내려받거나 임포트하지 않았습니다. 외부 라이선스는 아래 링크의 각 페이지에서 직접 확인한 문구만 적었고, 확인하지 못한 항목은 "미확인"으로 표시했습니다. 경로는 저장소 루트(`PrisonRunner/`) 기준입니다. `Game/`은 `Unity/Assets/Game/`을 줄인 것입니다.

> **결정(2026-11-11):** 1순위(자체 도형 통나무 가로대 `OBS_ZooLowLog`)와 연결 방법 (a)(z 19m 저작 행, 동물 대신 1/3 확률)를 채택했습니다. Yard(타입 11)의 숙이기 행도 같은 비주얼로 바꿨습니다. 외부 에셋·Kenney 복원·Quaternius는 쓰지 않았습니다.

## 요약

1. **1순위: 기존 `OBS_LowPipe` 구조에 동물원 재질 입히기 (외부 에셋 없음)**
   - 다리 2개와 1.52 m 높이 가로대로 된 숙이기 장애물이 이미 있습니다(`Game/Editor/MuhanokArtUpgrade.Maps.cs:22-25`).
   - 동물원 청크 프리팹에도 이 장애물 행이 들어 있지만 꺼져 있습니다(아래 "현재 구조" 참고).
   - 동물원 통나무 재질(`ZooLogBark`/`ZooLogHeart`, `Game/Editor/MuhanokArtUpgrade.Zoo.cs:77-78`)과 펜스 기둥 형태(`Maps.cs:305`)를 쓰면 됩니다. 예를 들어 "통나무 가로대 + 나무 기둥 + 동물 횡단 표지판"이나 "가로대에 매달린 사육사 먹이 바구니"를 만들 수 있습니다.
   - 기존 동물원 장애물처럼 코드 프리미티브로 만들므로 라이선스 문제가 없고 크기·피벗도 이미 검증돼 있습니다.
2. **2순위: Kenney Nature Kit의 `log_large`와 `stump_*` 등 (CC0, 이미 쓰는 키트)**
   - 2026-10-29 정리 때 지운 모델이지만 Git `d78d078^`에서 그대로 되살릴 수 있습니다.
   - 쓰러진 통나무를 그루터기나 펜스 기둥 위에 걸쳐 놓으면 숙이기 장애물이 됩니다. 이미 배치 중인 `tree_detailed_dark`, `stone_largeA`와 같은 스타일입니다.
   - 단, 의도적으로 지운 리소스를 되살리는 일이므로 보존 목록 `FREE_ASSETS_IMPORTED.json`도 함께 고쳐야 합니다.
3. **3순위: Quaternius 단일 모델 `Vines`/`Banner`/`Wood Log` (Poly Pizza, CC0)**
   - 1·2순위 가로대에 덩굴이나 현수막을 매달면 "동물원다움"이 더 살아납니다.
   - Quaternius는 새 출처라서 `THIRD_PARTY_NOTICES.md`에 항목을 추가해야 합니다.

대안으로 기존 `FallenTreeHazard`를 바꿔 쓰는 방법도 있습니다. 나무가 펜스 높이에 걸려 멈추게 하면 됩니다. 이 방법은 새 에셋 없이 연출을 재사용하지만 회전축 높이와 판정 종류를 바꾸는 코드 작업이 필요합니다(후보 표 P4).

## 현재 구조

### 숙이기 판정 방식

- **판정 종류:** 숙이기 장애물은 `ObstacleKind.Pipe`입니다(`Game/Domain/RunRules.cs:8`). 행동 매핑은 `Pipe → HazardAction.Crouch`입니다(`Game/Domain/ObstacleLayout.cs:50-51`).
- **피했는지 판단:** `Pipe`는 웅크린 상태일 때만 통과로 칩니다. 점프로는 피할 수 없습니다(`RunRules.cs:128`). 숙이기 지속 시간은 0.8초입니다(`RunRules.cs:40`).
- **물리 콜라이더가 없습니다.** `ObstacleController`가 다음 셋만 봅니다(`Game/Presentation/Map/ObstacleController.cs:13-23`).
  - 러너가 장애물 z 평면을 지나는 순간인지
  - 같은 차선인지(폭 ±0.95 m, 전체 행이면 ±1.101 m)
  - `Avoided()`가 참인지
  - 따라서 메시 모양은 게임 규칙에 영향을 주지 않습니다. 이는 AGENTS.md의 "Visual Mesh와 Collider 분리" 원칙과 같습니다.
- **기존 숙이기 비주얼**
  - `OBS_LowPipe` 원형: 1.9×0.35×0.6 m 상자를 높이 1.5 m에 둔 형태입니다(`Game/Editor/MuhanokContentBuilder.cs:57`, `:288`).
  - 현재 아트: 지름 0.35 m 파이프를 y 1.52 m, x ±0.97 m 사이에 걸치고, 다리 2개와 칼라를 붙인 형태입니다(`MuhanokArtUpgrade.Maps.cs:22-25`).
  - 감옥 입구의 `ClosingSlideGate`도 `Pipe`입니다. 문 하단이 0.95 m까지 내려와 그 아래로 숙여 지나갑니다(`Game/Presentation/Map/ClosingSlideGate.cs:9`, `Game/Editor/MuhanokArtUpgrade.SlideGate.cs:16`).
- **화면 안내:** `Kind == Pipe`이면 "↓ 숙이기!"가 자동으로 표시됩니다. 단 `ZooHazard`의 자식이면 무조건 "↑ 점프!"가 표시됩니다(`Game/Presentation/UI/ActionCuePresenter.cs:52`). 새 숙이기 장애물을 `ZooHazard` 아래에 두면 안 됩니다(예: 기린 목을 머리 위로 뻗는 연출).
- **자동 리뷰·녹화:** `Kind == Pipe`이면 숙이기 명령을 보냅니다(`Game/Bootstrap/RuntimeVisualReview.cs:81`, `RuntimeGameplayRecording.cs:185`, `RuntimeExpansionReview.cs:263`).

### 걷기 청크의 숙이기 행

- 버스·광산 수레 구간(타입 1·2)이 아닌 걷기 청크에는 `MapExercises` 행이 2개 있습니다.
  - z 7 m: 점프 행(`OBS_Crate_Low`)
  - z 19 m: 숙이기 행(`OBS_LowPipe`)
  - 각 행은 차선 3개 전체에 걸칩니다(`Game/Editor/MuhanokGameplayUpgrade.cs:42-47`).
- 동물원 청크 `CH_OuterWall`(타입 12) 프리팹에도 `Exercise_1_-1/0/1` 소켓과 Pipe 비주얼이 있습니다(`Game/Content/Prefabs/CH_OuterWall.prefab`, `Kind: 1` 항목).

### 동물원은 숙이기 행을 고르지 않습니다

- 확장 경로의 동물원 갈림길 바퀴는 거의 모두 타입 12입니다(`Game/Domain/RoutePlan.cs:102-106`).
- `ObstacleLayout.Plan`에서 타입 12는 둘 중 하나만 고릅니다(`ObstacleLayout.cs:81-87`). 그래서 저작된 점프·숙이기 행이 켜지지 않습니다.
  - 차선 막기 블로커. 이 확률은 다른 맵의 0.52/0.72 배입니다(`:29`).
  - 그 밖의 경우 `Animal = true`
- `Animal`이면 `RuntimeContent.Decorate`가 청크 장애물과 운동 행을 모두 끄고, z 12 m에 하나를 놓습니다(`Game/Presentation/Map/RuntimeContent.cs:130-150`).
  - 기린, 코끼리, 쓰러지는 나무 중 하나입니다. `sequence % 3`으로 고릅니다.
  - 셋 다 `ObstacleKind.Crate`, 즉 점프 장애물입니다(`Game/Presentation/Map/RuntimeContent.Animals.cs:64`, `:92`, `:111`).
- 충돌 간격 계산에서도 `Animal`은 Jump로 다룹니다(`ObstacleLayout.cs:154`, `:175`).
- 단일 청크 `CH_Yard`(타입 11)는 일반 행 규칙을 따릅니다. 그래서 이미 철제 `OBS_LowPipe` 숙이기 행이 나옵니다(`CH_Yard.prefab`의 `Exercise_1_*` 3개, `Collar` 6개). 동물원 외관인데 공장 파이프라는 점이 이번 작업의 시각적 근거이기도 합니다.
- 아트 문서도 동물원 장애물을 "점프 장애물 행"으로 규정합니다(`docs/ART_DIRECTION.md:26`).

### 새 동물원 숙이기 장애물의 필요 형태

| 항목 | 기준 | 근거 |
| --- | --- | --- |
| 폭 | 차선당 1.9~2.2 m. 전체 행이면 차선 중심 x = -2.2/0/2.2에 1개씩 3개, 또는 약 6.6 m짜리 1개 | 차선 간격 2.2 m(`RunRules.cs:39`), 배치 위치 `(i-1)*2.2`(`RuntimeContent.cs:365-373`) |
| 아래 여유 높이 | 아랫면 약 0.95~1.35 m. 숙인 시점 카메라(0.85 m)보다 위, 서 있는 눈높이(1.65 m)보다 아래 | 슬라이드 게이트 0.95 m(`ClosingSlideGate.cs:9`), 파이프 아랫면 1.52−0.175 m(`Maps.cs:24`), 카메라 1.65 m(`MuhanokContentBuilder.cs:80`)와 숙이기 시 −0.8 m(`Game/Presentation/Camera/FirstPersonCameraRig.cs:64`) |
| 윗부분 | 점프 정점이 1.8 m입니다. 규칙상 점프로는 피할 수 없으므로 기둥·간판이 1.8 m보다 높아 보여야 "점프하면 된다"는 오해가 없습니다 | `RunRules.cs:40`, `:128` |
| 깊이·z | 판정은 한 z 평면에서 합니다. 두께 0.3~0.8 m의 얇은 형태가 좋습니다. 위치는 `CentreZ` 12 m(Animal 자리) 또는 저작 행 19 m | `ObstacleLayout.cs:27`, `MuhanokGameplayUpgrade.cs:46` |
| 피벗 | 차선 중심의 바닥(y = 경로 표면)에 둡니다. 편집 도구 `Prop()`가 높이에 맞춰 크기를 조정하고 bounds 바닥을 피벗에 맞추므로, 원본 FBX의 피벗과 단위는 상관없습니다 | `Game/Editor/MuhanokArtUpgrade.cs:110-114` |
| 성능 | 풀링 대상입니다. 같은 재질 조각은 `CombineParts`로 합칩니다 | `RuntimeContent.cs:337-353` |

### 연결 방법 (구현 선택지, 결정 필요)

- **(a) 저작 행 활용**
  - 타입 12에서도 `Animal` 대신 일정 확률로 운동 행 1(z 19 m)을 켭니다(`ObstacleLayout.cs:87` 분기 수정).
  - `AddZooHazards`나 `Configure`에서 이 행의 비주얼만 동물원풍으로 바꿉니다(`MuhanokArtUpgrade.Zoo.cs:67-88`).
  - 기존 `Conflicts`와 `Violation` 규칙을 그대로 씁니다.
- **(b) 런타임 장애물 추가**
  - `ObstacleLayout`에 Animal과 같은 급의 플래그(예: Overhead, Crouch 행동)를 추가합니다.
  - `RuntimeContent`에 동물원 숙이기 오브젝트 풀을 만듭니다(기린·나무와 같은 방식).
  - `Conflicts`/`Violation`의 Animal 처리(`:154`, `:175`)를 행동별로 일반화해야 합니다.
- 어느 쪽이든 다음 테스트를 갱신해야 합니다.
  - `Game/Tests/EditMode/ObstacleLayoutTests.cs:134` 부근의 전체 행·동물원 규칙
  - `Game/Tests/PlayMode/ZooEncounterTests.cs:29`의 동물 8마리 전제

## 프로젝트 내 후보

| # | 에셋 경로 | 무엇인가 | 숙이기 장애물로 쓰는 법 | 라이선스·출처 | 주의 |
| --- | --- | --- | --- | --- | --- |
| P1 | `Game/Content/Prefabs/OBS_LowPipe.prefab` (아트: `MuhanokArtUpgrade.Maps.cs:22-25`) | 기둥 2개와 1.52 m 가로대 | 형태와 높이를 그대로 두고 재질만 동물원 통나무·대나무 색으로 바꿉니다. 노란·검정 줄무늬 "머리 조심" 판을 추가합니다 | 자체 제작(코드 프리미티브) | 공용 프리팹이라 직접 바꾸면 광산·감옥도 바뀝니다. 동물원용 변형을 따로 만들어야 합니다 |
| P2 | `Game/Content/ArtV2/Materials/ZooLogBark.mat`, `ZooLogHeart.mat` | 동물원 쓰러진 통나무 재질 | P1 가로대를 통나무로 바꾸고 자른 면에 Heart 재질을 씁니다(`MuhanokArtUpgrade.Zoo.cs:77-80`의 FallenLog 방식) | 자체 생성 재질(`References/GeneratedV6/Provenance.md`, `docs/ASSET_CATALOG.md:10`) | 지금은 동물원 맵 생성 시 고정 통나무 비주얼에 쓰입니다(`Zoo.cs:79`) |
| P3 | 펜스 기둥·레일 형태(`MuhanokArtUpgrade.Maps.cs:305-308`, `FencePost`/`FenceRail`, 재질 `iron`) | 동물원 울타리 | 길 양옆 펜스와 같은 기둥 2개 위에 레일이나 통나무를 걸쳐 "울타리 사이 낮은 가로대"를 만듭니다 | 자체 제작 | 철제라 어두운 맵에서 가로대가 묻힐 수 있습니다. 가독성 기준(`ART_DIRECTION.md:7`)에 따라 밝은 색 띠를 추가해야 합니다 |
| P4 | `Game/Presentation/Map/FallenTreeHazard.cs`, `RuntimeContent.Animals.cs:94-111` | 다가가면 쓰러지는 나무(현재 점프용 Crate) | 회전축을 펜스 상단 높이(약 1.5 m)로 올리고 각도를 조정해 나무가 길 위에 걸린 채 멈추게 합니다. `Kind=Pipe`로 바꿉니다 | 자체 제작 | 연출 재사용이라 임팩트가 큽니다. 하지만 `RuntimeZooReview`(`Game/Bootstrap/RuntimeZooReview.cs:50-81`)와 `ZooEncounterTests`가 기대하는 동작이 바뀝니다. 기존 점프 나무와 모양이 비슷해 헷갈리지 않도록 색이나 안내판을 달리해야 합니다 |
| P5 | `Unity/Assets/External/Staging/Kenney/nature-kit/Models/FBX format/tree_detailed_dark.fbx`, `tree_palmDetailedTall.fbx` | 이미 배치 중인 Kenney 나무 | 비스듬히 눕혀 가지를 가로대로 쓰거나 P1 기둥 장식으로 씁니다 | Kenney Nature Kit, CC0 (`docs/FREE_ASSETS_IMPORTED.json:49-64`) | 나무 전체를 눕히면 실루엣이 불분명합니다. 장식용으로만 권장합니다 |
| P6 | `Unity/Assets/External/Staging/Blender/Models/ZooGiraffe.fbx`, `ZooElephant.fbx` | 자체 Blender 동물(울타리 장식, `Maps.cs:301`) | 가로대 옆 장식으로 둡니다(예: 기린이 가로대 너머로 고개를 내민 모습) | 자체 제작(`THIRD_PARTY_NOTICES.md:19`) | `ZooHazard`로 만들면 안내 문구가 "점프"로 고정됩니다(`ActionCuePresenter.cs:52`). 장식 전용으로만 써야 합니다 |
| P7 | `Unity/Assets/External/Staging/Kenney/survival-kit/Models/FBX format/box-large.fbx` | 나무 상자(동물원 Barrier 비주얼, `Zoo.cs:85`) | 가로대에 매단 "먹이 상자" 장식 | Kenney Survival Kit, CC0 (`FREE_ASSETS_IMPORTED.json:66-80`) | 상자는 점프·회피 장애물을 연상시킵니다. 높이 위치로 구분해야 합니다 |
| P8 (복원 필요) | Git `d78d078^:./Unity/Assets/External/Staging/Kenney/nature-kit/Models/FBX format/` 아래 `log_large.fbx`, `log.fbx`, `stump_oldTall.fbx`, `stump_squareDetailedWide.fbx`, `fence_simpleHigh.fbx`, `fence_gate.fbx`, `sign.fbx` | Kenney 통나무·그루터기·펜스·표지판 | 키 큰 그루터기 2개(또는 `fence_simpleHigh` 2개) 위에 `log_large`를 걸치고 `sign`을 붙입니다. 1·2순위 조합입니다 | Kenney Nature Kit, CC0 ([kenney.nl/assets/nature-kit](https://kenney.nl/assets/nature-kit) 페이지 표기: "Creative Commons CC0") | **2026-10-29 정리에서 의도적으로 지운 모델입니다**(`docs/reports/ASSET_CLEANUP_2026_10_29.json`의 `kenneyFbx: 708`, 커밋 `d78d078`). 되살리려면 `FREE_ASSETS_IMPORTED.json`의 `retained_assets`에 추가하고 `.meta`도 함께 관리해야 합니다. `import_free_kits.py`는 보존 목록에 없는 모델을 다시 가져오지 않습니다(`Tools/AssetPipeline/import_free_kits.py:28-31`). Kenney 이름의 `_fall`은 쓰러짐이 아니라 가을 색 변형입니다 |
| P9 (복원 필요) | 같은 커밋의 survival-kit `structure.fbx`, `structure-canvas.fbx`, `signpost.fbx`, `tree-log.fbx` | Kenney 나무 골조·천막·이정표 | 나무 골조 위에 천막을 올려 "낮은 차양"을 만듭니다 | Kenney Survival Kit, CC0 ([kenney.nl/assets/survival-kit](https://kenney.nl/assets/survival-kit) 페이지 표기: "Creative Commons CC0") | P8과 같은 복원 절차가 필요합니다. 골조가 바닥까지 닫히면 회피 장애물처럼 보이므로 아래를 비워야 합니다 |

참고로 `External/Staging/Blender/Models/CaveArch.fbx`(광산 아치, `MuhanokArtUpgrade.Maps.cs:132`)는 광산 테마라 제외했습니다.

## 외부 무료 후보

Poly Pizza 검색 목록에는 개별 라이선스가 표시되지 않습니다. 그래서 각 모델 페이지를 직접 열어 확인했습니다.

| 이름 | URL | 작성자 | 라이선스(페이지 표기) | 형식 | 아트 방향 적합도 | 주의 |
| --- | --- | --- | --- | --- | --- | --- |
| Kenney Nature Kit (P8 원본 키트) | https://kenney.nl/assets/nature-kit | Kenney | "Creative Commons CC0" | 페이지에 형식 표기 없음. 기존 보존 경로상 FBX 포함 | 매우 높음: 이미 사용 중인 키트(`FREE_ASSETS_IMPORTED.json:49-64`) | 새 다운로드 없이 Git에서 복원할 수 있습니다. 개별 모델 목록은 페이지에 없고 Git 삭제 기록으로 확인했습니다 |
| Kenney Survival Kit (P9 원본 키트) | https://kenney.nl/assets/survival-kit | Kenney | "Creative Commons CC0" | 페이지에 형식 표기 없음 | 매우 높음: 이미 사용 중 | 위와 같습니다 |
| Quaternius "Wood Log" | https://poly.pizza/m/L4E32Wee6C | Quaternius | "Public Domain (CC0)" | FBX, GLTF | 높음: 로우폴리 스타일라이즈드 | Quaternius는 새 출처라 NOTICES에 항목을 추가해야 합니다. 폴리곤 수는 페이지에 없습니다 |
| Quaternius "Wood Log with Moss" | https://poly.pizza/m/nwsYvcI0bC | Quaternius | "Public Domain (CC0)" | FBX, GLTF | 높음: 이끼가 동물원 정글 느낌에 맞음 | 위와 같습니다 |
| Quaternius "Vines" | https://poly.pizza/m/EVS4viM9BL | Quaternius | "Public Domain (CC0)" | FBX, GLTF | 높음: 가로대에 매다는 덩굴 커튼 | 덩굴만으로는 "단단한 장애물"로 읽히지 않습니다. 반드시 가로대와 조합해야 합니다 |
| Quaternius "Banner" | https://poly.pizza/m/Kd94xlw5aj | Quaternius | "Public Domain (CC0)" | FBX, GLTF | 중간: 판타지 깃발 형태일 수 있음(미리보기 미검수) | 동물원 문구는 기존 `Label`(`MuhanokArtUpgrade.cs:257`)로 덧입혀야 합니다 |
| Quaternius "Town Sign" | https://poly.pizza/m/VSZubp0ru3 | Quaternius | "Public Domain (CC0)" | FBX, GLTF | 중간 | 가로형 간판이라면 머리 위 "LOW CLEARANCE" 판으로 쓸 수 있습니다. 형태는 미검수입니다 |
| Quaternius Stylized Nature MegaKit | https://quaternius.com/packs/stylizednaturemegakit.html | Quaternius | "CC0" | FBX, OBJ, Blend, glTF | 높음 | 통나무·가지가 들어 있는지는 페이지에 목록이 없어 **미확인**입니다 |
| Quaternius Ultimate Nature Pack | https://quaternius.com/packs/ultimatenature.html | Quaternius | "CC0" | FBX, OBJ, Blend | 높음 | 모델 150개. 개별 목록은 **미확인**입니다 |
| Quaternius Ultimate Stylized Nature Pack | https://quaternius.com/packs/ultimatestylizednature.html | Quaternius | "CC0" | FBX, OBJ, Blend, glTF | 높음 | 모델 63개. 개별 목록은 **미확인**입니다 |
| Kenney Fantasy Town Kit | https://kenney.nl/assets/fantasy-town-kit | Kenney | "Creative Commons CC0" | 페이지에 표기 없음 | 높음: Kenney 형제 키트 | 가로대·아치 모델이 있는지 **미확인**입니다(페이지에는 "wall" 태그만 있음) |
| "Zoo Entrance" | https://www.blendswap.com/blend/4461 | zerba | "CC-BY" | .blend (Blender 2.6x, Blender Internal) | 중간: 동물원 입구 아치 | 출처 표기가 필요합니다. 크레딧 화면이나 NOTICES에 추가해야 합니다. 높은 입구라 숙이기 높이로 줄이면 비율이 깨집니다. 오래된 Blender 형식이라 변환이 필요합니다. 폴리곤 수는 미기재입니다 |
| "Canopy" | https://poly.pizza/m/1EIz0tpCwPH | Poly by Google | "Creative Commons Attribution"(CC BY 3.0 링크) | OBJ, GLTF | 낮음~중간 | 출처 표기가 필요합니다. 스타일 일관성이 불확실합니다 |

## 제외한 것과 이유

- **Unity Asset Store 무료 에셋**
  - 표준 EULA는 에셋을 "Licensed Product에 포함·내장(embedded)"한 상태로만 배포를 허용합니다(https://unity.com/legal/as-terms §2.2.1(a)(b)). 사용자의 재배포·양도도 금지합니다(§2.2.1.1(b)).
  - 이 프로젝트는 소스를 GitHub에 공개하고 공개 릴리스 기록도 있습니다(`docs/PUBLIC_RELEASE.md`). 원본 에셋 파일이 저장소에 들어가면 Mixamo와 같은 별도 처리가 필요하므로 제외했습니다.
- **Mixamo 원본 FBX:** 공개 재배포에서 제외하는 기존 규칙입니다(`THIRD_PARTY_NOTICES.md:11`, `AGENTS.md`).
- **CGTrader, RenderHub, Fab 등 동물원 팩:** 검색 결과에서 유료이거나 마켓 고유 라이선스였습니다(재배포 조건 미확인). 기본적으로 제외했습니다.
- **Sketchfab:** 이번에는 개별 모델 페이지와 라이선스를 확인하지 못했습니다. 후보에 넣지 않았습니다(**미확인**).
- **OpenGameArt:** 이번 조사에서 확인한 페이지가 없습니다(**미확인**).
- **Poly Pizza의 "Poly by Google" 모델들:** CC-BY 3.0이고 작성자가 일괄 표기돼 스타일 편차가 큽니다. "Canopy" 하나만 참고로 남겼습니다.
- **`CaveArch.fbx`, factory-kit 파이프류:** 광산·공장 테마라 동물원 가독성 목표에 맞지 않습니다.
- **2026-10-29에 지운 Kenney 모델:** P8·P9로 올렸지만 복원은 정리 결정을 일부 되돌리는 일입니다. 사용자 승인이 필요합니다.

## 열린 질문

1. 구현은 (a) 동물원 청크에 이미 있는 z 19 m 숙이기 행을 켜는 방식과 (b) 기린·나무처럼 런타임 풀 장애물을 추가하는 방식 중 어느 쪽인가요? (b)는 `Conflicts`/`Violation`과 테스트 변경 범위가 더 큽니다.
2. 동물원 장애물 비율을 어떻게 할까요? 지금은 동물(점프)과 블로커만 나옵니다. 숙이기를 동물 대신 일정 확률로 넣을지, 점프 동물 뒤 6 m 이상 간격(`ActionSpacing`)을 두고 추가할지 정해야 합니다.
3. 2026-10-29에 지운 Kenney Nature/Survival 모델(`log_large`, `stump_*`, `sign`, `structure-canvas`)을 되살려도 되나요? 아니면 자체 프리미티브(P1~P3)만 쓸까요?
4. 새 출처(Quaternius CC0)를 `THIRD_PARTY_NOTICES.md`에 추가해도 되나요? CC-BY 에셋을 쓴다면 출처 표기 위치는 게임 내 크레딧과 NOTICES 중 어디로 할까요?
5. 감옥에서 동물원으로 넘어가는 `CH_Yard`(타입 11)의 철제 파이프 숙이기 행도 동물원풍으로 바꿀까요?
6. 쓰러지는 나무를 "걸린 나무" 숙이기로 바꾸는 안(P4)은 기존 점프용 나무와 헷갈릴 수 있습니다. 두 가지를 모두 유지할까요?
