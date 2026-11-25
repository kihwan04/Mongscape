# 관절 녹화와 인식 점수표

웹캠으로 동작을 녹화해 두고, 판정 기준값을 바꿀 때마다 같은 녹화를 다시 돌려 동작별 정답률과 오인식 횟수를 숫자로 비교합니다. 녹화에는 **관절 좌표만** 들어갑니다. 영상이나 사진은 저장하지 않습니다.

## 팀원이 녹화하는 방법

1. 에디터나 개발 빌드(Development Build)로 실행합니다. 정식 빌드에는 녹화 기능이 없습니다.
2. 탈출 준비에서 전신(또는 상체) 방식을 고르고 판을 시작합니다. 카메라에서 2~3m, 카메라는 허리 높이, 머리부터 발끝까지 보이게 섭니다. 보정 화면의 안내가 사라지면 됩니다.
3. **F6**을 누르면 녹화가 시작되고 화면 위쪽에 "● 관절 녹화 중"이 뜹니다. 판 중이든 메뉴든 카메라가 켜져 있으면 됩니다. 시험용이면 F9 무적을 켜 두면 편합니다.
4. 처음 **3초는 가만히 서 있습니다.** 점수표가 이 구간으로 보정합니다.
5. 동작을 하고, 각 동작의 시작·끝 시각을 적어 둡니다 (폰 스톱워치를 F6과 함께 누르면 쉽습니다).
6. 다시 **F6**을 누르면 저장됩니다. 저장 위치는 로그에 나오며 보통 `%USERPROFILE%\AppData\LocalLow\<회사>\<제품>\PoseRecordings\`입니다. `20261125-153012-FullBody.pose.csv.gz`와 빈 라벨 파일 `20261125-153012-FullBody.labels.csv`가 생깁니다.
7. 라벨 파일에 동작과 시각을 적고, 두 파일을 `Tools/QA/PoseRecordings/team/`에 넣습니다. 파일 이름은 `이름-동작.pose.csv.gz`처럼 바꿔도 되지만 두 파일의 앞부분은 같아야 합니다.

녹화 파일 머리의 `person=`, `note=` 줄은 비어 있습니다. 누가 어떤 조건에서 찍었는지 적어 두면 좋습니다 (실명 대신 별명). 얼굴이나 영상은 들어 있지 않지만, 녹화한 사람에게 저장소에 올려도 되는지 확인하고 넣습니다.

### 무엇을 녹화하나 (기획서 9장)

한 사람당 동작별 약 30초. 동작 사이에는 2~3초 가만히 섭니다.

| 녹화 | 하는 것 | 라벨 |
| --- | --- | --- |
| 걸음 | 제자리 걷기, 분당 약 90걸음 | `walk` |
| 달리기 | 제자리 뛰기, 분당 약 130걸음 | `run` |
| 전력 | 가장 빠르게 제자리 뛰기 (점프 오인식 확인용, 가장 중요) | `sprint` |
| 점프 | 2~3초마다 한 번씩 점프 10회 | 점프마다 `jump` 한 줄 |
| 달리며 점프 | 달리다가 점프, 다시 달리기 | `run` 구간과 `jump` |
| 숙이기 | 1초씩 숙였다 일어나기 8회 | 숙일 때마다 `crouch` |
| 무릎 | 무릎 높이 들기 (계단처럼) | `knee` 구간 + 같은 구간 `stairs`, 넷째 칸에 든 횟수 |
| 사이드 스텝 | 왼쪽·오른쪽으로 크게 한 발씩 각 5회 | `side_left` / `side_right` |
| 상체 동작 | 상체 방식으로 고르고 같은 순서 | 같은 라벨 (녹화의 `style=UpperBody`) |

## 라벨 파일

`<이름>.labels.csv`. 한 줄에 `동작,시작초,끝초[,횟수]`. 시각은 녹화 시작(F6)부터의 초입니다. `#`로 시작하는 줄은 무시합니다.

```text
# action,start,end[,count]
run,3.0,33.0
jump,36.2,36.9
knee,40,50,14
stairs,40,50
```

| 라벨 | 뜻 | 채점 |
| --- | --- | --- |
| `stand` | 가만히 서 있음 | 분당 걸음이 "서 있음"으로 읽힌 프레임 비율 |
| `walk` / `run` / `sprint` | 걸음(100 이하) / 달리기(100~150) / 전력(150 이상) | 그 단계로 읽힌 프레임 비율. 구간의 처음 2초는 빼고 셈 |
| `jump` | 점프 한 번 (발이 뜬 구간을 넉넉히) | 구간 ±0.3초 안의 점프 인식 = 맞힘, 라벨 밖 점프 = 오인식 |
| `crouch` | 숙이기 한 번 (숙인 구간) | 같은 방식 |
| `knee` | 무릎 들기 구간, 넷째 칸은 든 횟수 (없으면 1) | 구간 안 무릎 인식 수, 횟수를 넘으면 오인식 |
| `side_left` / `side_right` | 플레이어 기준 왼쪽 / 오른쪽 사이드 스텝 한 번 | 레인 변경 인식 |
| `stairs` / `cart` | 상황 표시. 게임이 계단 / 수레 구간일 때와 같게 판정 | 채점 안 함 |

무릎 인식은 계단에서만 쓰이므로 `stairs`나 `knee` 구간 밖의 무릎 인식은 세지 않습니다. 제자리 달리기(`walk`/`run`/`sprint`) 구간 안의 점프는 "제자리 달리기 중 잘못된 점프"로 따로 셉니다. 합격 기준 출발점: 전력 1분에 잘못된 점프 0회, 진짜 점프 인식률 95% 이상.

## 녹화 파일 형식

`<이름>.pose.csv` 또는 gzip으로 묶은 `<이름>.pose.csv.gz` (F6 녹화는 끝날 때 gzip으로 저장). UTF-8 텍스트입니다.

```text
# mongscape-pose 1
# source=webcam
# style=FullBody
# camera=640x480@30
# device=USB Camera
# model=full
# inference_fps=30
# created=2026-11-25T15:30:12+09:00
# app=1.0.0
# person=
# note=
t,has,aspect,scale,x0,y0,z0,v0,...,x32,y32,z32,v32
0,1,1.3333,0.52,0.5012,0.2481,-0.412,0.998,...
```

- 첫 줄은 꼭 `# mongscape-pose 1`.
- `# 키=값` 줄은 메타데이터: `source`(webcam / synthetic), `style`(FullBody / UpperBody), 카메라 해상도·fps, 장치 이름, 랜드마크 모델(lite / full), 만든 시각, 앱 버전, 녹화한 사람과 메모.
- 한 행이 카메라 프레임 하나: `t` 녹화 시작부터의 초, `has` 사람이 잡혔으면 1, `aspect` 화면 비율, `scale` 추적한 몸 영역 크기(필터용), 그리고 BlazePose 관절 33개의 `x, y, z, v`.
- `x, y`는 0~1, 거울 화면 기준(x는 플레이어의 오른쪽으로 커짐, y는 아래로 커짐). `z`는 모델이 준 깊이 값 그대로(판정에는 안 씀). `v`는 신뢰도.
- 필터 전 값입니다. 재생할 때 게임과 같은 One Euro 필터와 `MotionClassifier`를 거칩니다.
- `has=0`인 행은 `t,has,aspect,scale` 네 칸만 있습니다.

## 점수표 돌리기

- **테스트**: Unity Test Runner의 EditMode `PoseScorecardTests.ScorecardOverTheRecordingFolder`. `Tools/QA/PoseRecordings/` 아래(하위 폴더 포함) 라벨이 있는 녹화를 모두 돌리고, 결과를 `Unity/Logs/PoseScorecard.md`와 테스트 출력에 씁니다. 라벨 파일이 없는 녹화는 건너뛰고 목록에 적습니다.
- **메뉴**: **Mongscape → Pose Scorecard**는 `GameSettings.asset`의 실제 Motion 값으로 같은 표를 만듭니다. **Mongscape → Pose Scorecard Sweep (jump)**은 무릎 대칭 기준과 들썩임 배수 여러 조합을 한꺼번에 돌려 `Unity/Logs/PoseScorecardSweep.md`에 비교표를 씁니다.
- 코드에서: `PoseRecordingFiles.LoadFolder` → `PoseRecordingFiles.Score(entries, tuning)` → `PoseScorecard.Report(results)`. `MotionTuning` 값을 바꿔 가며 부르면 다른 조합도 비교할 수 있습니다.

결과 표는 실제 웹캠 녹화와 합성 녹화를 나눠 보여 줍니다. **합성 녹화 줄에는 `[합성]`이 붙고 실제 인식 정확도가 아닙니다.**

## 폴더

| 폴더 | 내용 |
| --- | --- |
| `Tools/QA/PoseRecordings/synthetic/` | 합성 녹화 5개 (`source=synthetic`). 스크립트로 만든 막대 몸에 관절 흔들림, 달리기 들썩임, 한쪽 무릎 들기를 넣은 것. 점수표가 돌아가는지와 스크립트한 경우(무거운 전력 제자리 달리기에서 점프가 안 잡히는지 등)를 확인하는 용도. `SyntheticPoseRecordings.cs`로 만들고, `MONGSCAPE_WRITE_POSE_SEEDS=1`로 `SyntheticSeedRecordingsCanBeRegenerated` 테스트를 돌리면 다시 씁니다 |
| `Tools/QA/PoseRecordings/team/` | 팀원 실제 녹화를 넣는 자리 |

녹화 하나(30초)는 gzip으로 약 0.3~0.5MB입니다.
