# Mongscape 중앙 얼굴 서버 · PostgreSQL + Docker

**2026-10-29: PR #20과 기존 개선을 통합한 소스입니다. develop 병합만 진행하며 새 릴리스·태그·서버 배포는 하지 않습니다. 기존 공개 ZIP은 통합 이전 버전입니다.**

여러 게임 PC가 하나의 얼굴 로그인 서버와 PostgreSQL DB를 공유합니다. 게임 PC에는 Python·Docker·PostgreSQL을 설치하지 않습니다. 몸동작 추론은 각 PC의 Unity에서 계속 처리합니다.

```text
게임 PC들 → HTTPS/Caddy → 얼굴 API(Python·OpenCV·Psycopg 3) → PostgreSQL 17
                                                               ↓
                                                       postgres-data volume
```

기존 [공개 릴리스](https://github.com/kihwan04/PrisonRunner/releases/tag/v2026.10.26)의 서버 ZIP은 계정 v2 통합 이전 파일입니다. 현재 서버는 이 브랜치의 `Services/Identity` 소스를 기준으로 준비해야 하며 이번 작업에서 배포하지 않습니다. 실제 서버·도메인은 아직 배포하지 않았습니다. 실제 PostgreSQL 17.11에서 로그인/기록/HTTP/이전 테스트를 통과했지만, 이 PC에는 Docker가 없어 Linux 컨테이너 기동과 외부 HTTPS는 아직 검증하지 못했습니다.

## 1. 배포 파일과 비밀번호 준비

Docker Engine/Compose v2가 있는 Linux 서버에 이 ZIP을 풉니다. Docker Desktop의 Linux 컨테이너 모드도 사용할 수 있습니다. 이 폴더 안에서 명령을 실행합니다.

먼저 DB 관리자와 앱 계정의 비밀번호 파일을 만듭니다. 서버에 Python 3이 있으면:

```sh
python3 prepare_secrets.py
```

Python 설치 없이 Docker만 사용하려면 Linux 서버에서:

```sh
docker run --rm --user "$(id -u):$(id -g)" -v "$PWD:/work" -w /work python:3.12-slim-bookworm python prepare_secrets.py
```

`secrets/db_admin_password`, `secrets/db_app_password`가 생성됩니다. 값은 출력하지 않고 기존 파일은 덮어쓰지 않습니다. 비밀번호 폴더는 Git/ZIP/이미지에서 제외합니다. Linux에서는 폴더를 0700, 파일을 읽기 전용으로 두어 Compose가 비관리자 컨테이너 사용자에게 해당 파일을 마운트할 수 있도록 합니다.

**권한을 바꾸지 마세요: 폴더 `secrets`는 0700, 파일은 0444입니다.** Compose 비밀 파일은 서버의 권한을 그대로 컨테이너에 넘깁니다. 파일을 `chmod 400`(root만 읽기)으로 바꾸면 비관리자로 실행되는 API(uid 10001)와 DB 초기화 스크립트(`postgres` 사용자)가 비밀번호를 읽지 못합니다. 파일 보호는 root만 들어갈 수 있는 0700 폴더가 맡습니다.

```sh
chmod 700 secrets && chmod 444 secrets/*
```

DB의 `postgres` 관리자와 API의 `mongscape` 계정을 분리합니다. 앱 계정은 SUPERUSER/CREATEDB/CREATEROLE 권한이 없고 자기 DB의 테이블을 사용합니다. DB 포트 5432는 외부에 공개하지 않습니다.

## 2. 서버 PC에서 먼저 실행

외부 도메인 없이 Docker와 PostgreSQL을 확인하려면:

```sh
docker compose up -d --build --wait
docker compose ps
curl --fail http://127.0.0.1:8765/health
```

health 응답의 `ok`가 true이고 **`storage`가 `postgres`**인지 확인합니다. DB 준비 → API 준비 순서로 시작하며 health는 실제 DB 연결도 확인합니다. 이미 로컬 Python 서버가 8765를 사용한다면 해당 서버를 종료하거나 `.env`의 `IDENTITY_LOCAL_PORT`를 바꿉니다.

최초 이미지 빌드에서 Python/OpenCV/NumPy/Psycopg와 얼굴 모델을 내려받습니다. 얼굴 모델은 manifest의 SHA-256으로 검사합니다. Windows `.runtime`, 실제 얼굴 DB와 비밀번호는 이미지에 넣지 않습니다.

## 3. 여러 PC를 위한 HTTPS

실제 도메인의 DNS를 서버 공인 주소로 연결하고 TCP 80/443을 준비합니다. AAAA 레코드가 있다면 IPv6도 실제 서버로 연결돼야 합니다. `.env.example`을 `.env`로 복사해 도메인을 수정합니다.

```dotenv
IDENTITY_DOMAIN=faces.your-domain.com
IDENTITY_LOCAL_PORT=8765
```

도메인에는 `https://`나 경로를 붙이지 않습니다. 예시 주소는 실제 주소로 바꿉니다.

```sh
docker compose --env-file .env -f compose.yaml -f compose.https.yaml up -d --build --wait
docker compose --env-file .env -f compose.yaml -f compose.https.yaml logs --tail=100
curl --fail https://faces.your-domain.com/health
```

Caddy가 HTTPS 인증서 발급/갱신과 API 전달을 담당합니다. Compose 기동 성공 뒤에도 외부 HTTPS health로 DNS·인증서를 따로 확인합니다. API/Caddy는 얼굴 이미지나 토큰을 요청 로그에 기록하지 않습니다.

### 기동 실패 확인

`ps`로 실패한 서비스를 찾고 `logs <서비스>`로 원인을 봅니다. `caddy`가 `not started`이면 대개 `identity`가 정상이 아니어서 기다리는 것입니다.

| 증상 | 원인 | 조치 |
| --- | --- | --- |
| identity 로그 `Identity database connection/setup failed: PermissionError` | 비밀 파일 권한이 0400 등 | `chmod 444 secrets/*` 뒤 `up -d --force-recreate --wait` |
| postgres 로그 `User "mongscape" has no password assigned` 또는 `init-postgres: ... not readable` | DB를 처음 만들 때 비밀 파일을 읽지 못함. 초기화 스크립트는 새 volume에서 한 번만 실행 | **데이터가 없는 새 DB일 때만** `down -v`로 volume을 지우고 다시 `up`. 데이터가 있으면 지우지 말고 `docker compose exec postgres psql -U postgres -d mongscape`에서 `ALTER ROLE mongscape PASSWORD '...'`로 맞춤 |
| 서버 파일 권한만 고친 뒤 `up`이 바로 unhealthy | 설정이 바뀌지 않아 기존 컨테이너를 재사용함 | `up -d --force-recreate --wait` |
| Caddy 로그 `timeout`/`connection refused` | 80/443이 막혔거나 다른 웹서버(Apache 등)가 80을 사용 | 방화벽(`ufw`, 호스팅 콘솔)과 `systemctl disable --now apache2` 확인 |

`/health`의 `liveness:false`는 정상입니다. 서버 생존 여부가 아니라 얼굴 위조 방지(liveness detection) 기능이 없다는 표시입니다. 생존 여부는 `ok`로 봅니다.

## 4. 게임 PC 연결

2026-10-26 원격 연결을 지원하는 Windows 클라이언트 ZIP을 전부 풉니다. PostgreSQL 변경은 서버 내부이므로 같은 EXE와 게임 API를 사용합니다. 2026-10-25의 localhost 고정 실행본은 원격 설정을 지원하지 않습니다.

각 PC에서 EXE가 있는 `Game` 폴더에 `identity-server.json`을 둡니다.

```json
{
  "url": "https://faces.your-domain.com"
}
```

동봉한 `identity-server.example.json`을 복사하거나 다음 PowerShell 도구를 사용합니다.

```powershell
.\ConfigureClient.ps1 -ServerUrl 'https://faces.your-domain.com' -GameDirectory 'C:\Mongscape\Game'
```

환경 변수 `MONGSCAPE_IDENTITY_URL`이 파일보다 우선합니다. 개발용 Unity는 `Unity/identity-server.json`을 읽습니다. 설정은 게임 시작 때 읽으므로 변경 뒤 게임을 다시 실행합니다. 원격 주소는 HTTPS, 로컬 Docker 확인은 loopback HTTP만 허용합니다.

게임에 DB 주소·사용자·비밀번호를 넣지 않습니다. 게임은 얼굴 API만 호출합니다. 얼굴 등록/로그인은 선택 사항이며 서버 없이도 키보드와 몸동작 플레이를 사용할 수 있습니다.

## 5. PostgreSQL 직접 살펴보기

서버에서 DB 콘솔을 엽니다. 앱 계정으로 실행합니다.

```sh
docker compose exec postgres psql -U mongscape -d mongscape
```

```sql
\dt
SELECT COUNT(*) AS profiles FROM profiles;
SELECT COUNT(*) AS runs FROM runs;
SELECT id, score, distance, kcal, created_at FROM runs ORDER BY created_at DESC LIMIT 10;
\q
```

`schema_version`은 DB 구조 버전(현재 2), `profiles`는 닉네임(`name`, 중복 검사용 `name_key`)·몸무게·기본 플레이 방식·목표·코인·해금 아이템(`unlocked`)·튜토리얼 완료 여부·가입 시각·동의 버전, `face_samples`는 얼굴 특징 벡터, `sessions`는 토큰 해시·만료, `runs`는 운동 기록(모드·획득 코인·점수 버전·플레이 시간 포함)입니다. **얼굴 사진은 저장하지 않습니다.** 로그인·가입 응답의 `avatar`는 그 요청에서 받은 첫 장의 정렬 얼굴을 잘라 돌려준 값이며 DB와 로그에 남지 않습니다. 특징 벡터는 BYTEA, 동작별 기록은 JSONB, 시간은 Unix 초(BIGINT)입니다. 관련 프로필을 삭제하면 FK CASCADE로 특징·기록·세션도 삭제됩니다. 회원 기록·코인·해금 아이템과 회원 랭킹은 이 DB에 있고, 게스트 순위와 화면 설정은 각 게임 PC의 로컬 저장입니다.

## 6. DB 보존과 백업

DB는 `postgres-data` volume에 보존합니다. 컨테이너 재생성은 기록을 지우지 않습니다. `docker compose down -v`는 volume까지 삭제하므로 데이터 유지가 필요할 때 사용하지 않습니다.

```sh
docker compose exec -T postgres pg_dump -U postgres -d mongscape -Fc -f /tmp/mongscape.dump
docker compose cp postgres:/tmp/mongscape.dump ./mongscape-backup.dump
```

백업은 개인 얼굴 정보와 기록을 포함하므로 별도로 보관하고 Git/ZIP에서 제외합니다. 복구는 백업을 새 DB에 복원해 검증한 뒤 진행합니다.

비밀번호 초기화 스크립트는 새 DB volume의 최초 초기화 때만 실행됩니다. 이미 초기화한 DB에서 비밀번호 파일만 바꿔도 DB 암호가 자동 변경되지는 않습니다.

## 7. 기존 DB 갱신 (`migrate_account.py`)

현재 DB 구조는 **스키마 v2**(회원 계정)입니다. 서버는 시작할 때 `schema_version`을 확인합니다. 새 DB는 처음부터 v2로 만들고, 이전 버전 DB(v1: `schema_version` 표가 없음, 얼굴 사진 `profiles.avatar`가 남아 있을 수 있음)를 발견하면 **시작하지 않고** `Old identity schema (v1). Back up the DB, run migrate_account.py, then restart.`를 출력한 뒤 종료합니다.

`migrate_account.py`는 기존 DB를 v1에서 v2로 바꿉니다. 단계는 다음 순서입니다.

1. `profiles.avatar`(저장된 얼굴 사진) 삭제
2. `profiles`에 `name_key`, `weight`(65), `style`(0), `goal`(50), `coins`(0), `unlocked`(`[]`), `tutorial_done`(거짓) 추가
3. 닉네임 정리: 규칙(앞뒤 공백 제거 후 1~10자, 제어 문자 없음)에 맞게 자르고, 비어 있으면 `탐험가`로 둡니다. 정규화 값(공백 제거·소문자)이 겹치면 먼저 가입한 회원(가입 시각, 같으면 ID 순)이 이름을 유지하고 뒤 회원에게 `2`, `3`…을 붙입니다. 기존에 겹치지 않던 이름은 바뀌지 않습니다.
4. `name_key` UNIQUE 인덱스
5. `runs`에 `mode`(기존 기록은 NULL), `coins`(0), `score_version`(1), `play_seconds`(0)와 `runs_mode_score` 인덱스 추가. 기존 기록은 점수 버전 1이라 랭킹·최고 기록에 들어가지 않고, 칼로리 합계에는 들어갑니다.
6. `schema_version=2`

모든 단계를 한 트랜잭션으로 실행해 오류가 나면 전부 되돌리고, 이미 적용된 단계는 건너뛰므로 다시 실행해도 안전합니다(v2 DB에서는 아무것도 하지 않습니다). 결과로 적용한 단계 이름만 출력합니다. 연결 설정은 `server.py`와 같습니다(`--database-url` 또는 `IDENTITY_DATABASE_URL`, 없으면 PGHOST/PGPORT/PGDATABASE/PGUSER/PGPASSWORD_FILE).

**저장된 얼굴 사진 삭제와 닉네임 변경은 되돌릴 수 없습니다.** 반드시 백업 → 스크립트 → 재시작 순서로 진행합니다.

Docker(PostgreSQL) 서버:

```sh
docker compose stop identity
docker compose exec -T postgres pg_dump -U postgres -d mongscape -Fc -f /tmp/mongscape.dump
docker compose cp postgres:/tmp/mongscape.dump ./mongscape-before-migrate.dump
docker compose build identity
docker compose run --rm identity python migrate_account.py
docker compose up -d --wait
curl --fail http://127.0.0.1:8765/health
```

HTTPS 구성이면 `up`에 3장과 같은 `--env-file .env -f compose.yaml -f compose.https.yaml`을 붙입니다.

백업에는 이전 얼굴 사진이 그대로 남습니다. 서버 동작을 확인한 뒤 백업을 안전하게 삭제하거나 비공개로 보관하고 Git/ZIP에 넣지 않습니다.

## 8. 계정 API (스키마 v2)

모든 요청은 `Content-Type: application/json` POST입니다(`GET /health` 제외). 인증이 필요한 경로는 `Authorization: Bearer <token>`을 보냅니다. 오류는 `{ok:false, message}`와 상태 코드 400(값 오류·코인 부족)·401(인증 없음/만료)·404·409(닉네임 중복·이미 등록된 얼굴·이미 해금)·429(시도 제한)입니다.

| 경로 | 인증 | 요청 → 응답 |
| --- | --- | --- |
| `/name-check` | - | `{name}` → `{ok, available, message}`. 규칙 위반·중복도 200 + `available:false` |
| `/enroll` | - | `{name, consent:true, images:[3], weight?, style?, goal?}` → `{ok, token, avatar, profile}` |
| `/login` | - | `{images:[3]}` → `{ok, token, avatar, profile}` |
| `/profile` | 필요 | `{}` → `{ok, profile}` |
| `/profile/update` | 필요 | `{name?, weight?, style?, goal?, tutorial_done?}`(보낸 필드만 변경) → `{ok, profile}` |
| `/runs` | 필요 | `{id, distance, score, kcal, goal, activities:[5], mode, coins, play_seconds, score_version}` → `{ok, coins}`(적립 후 잔액) |
| `/shop/buy` | 필요 | `{item}` → `{ok, profile}` |
| `/leaderboard` | 선택 | `{mode, today}` → `{ok, entries:[{rank, name, score, distance, created_at}], me?:{rank, score, distance}}` |
| `/stats` | 필요 | `{}` → `{ok, today_kcal, total_kcal, best_score:[전신, 상체, 키보드]}` |
| `/logout`, `/delete`, `/history` | 필요 | 기존과 같음. `/delete`는 프로필·특징값·세션·기록 삭제 |

- `profile`: `{id, name, weight, style, goal, coins, unlocked, tutorial_done}`. `/enroll`·`/login` 응답은 이전 클라이언트를 위해 `id`, `name`도 최상위에 둡니다.
- 닉네임: 앞뒤 공백 제거 후 1~10자(유니코드 코드 포인트 기준, 이모지 1개 = 1자), 제어·서식(U+200B 등)·사용자 정의 문자 등 유니코드 C 범주 금지. 게임의 입력 검사도 같은 규칙입니다. 공백을 지우고 소문자로 바꾼 값이 같으면 같은 닉네임입니다(`Min Su` = `minsu`).
- 값 범위: `weight` 10~350(게임의 몸무게 입력과 같음, null이면 65), `style`·`mode` 0 전신/1 상체/2 키보드, `goal` 1~5000, `coins` 0~5000 정수, `score_version` 1 또는 2, `play_seconds` 0~86400.
- 아이템 가격: 0 자석 30, 1 코인 2배 60, 2 방패 100코인.
- `/runs`: 같은 기록 ID를 다시 보내면 저장·적립 없이 현재 잔액을 돌려줍니다. 기록 삽입과 코인 적립은 한 트랜잭션입니다. `mode`를 보내지 않은 이전 클라이언트의 기록도 저장하지만(모드 NULL) 랭킹에는 들어가지 않습니다.
- `/leaderboard`: 해당 모드의 `score_version=2` 기록 중 회원마다 최고 점수 1개로 상위 50명을 만듭니다. 동점이면 먼저 세운 기록이 앞섭니다. `today:true`는 한국 시간(KST) 0시 이후 기록만 봅니다. `name`은 현재 닉네임입니다. `me`는 유효한 토큰이 있고 해당 모드 기록이 있을 때만 들어가며, 만료된 토큰은 오류 없이 무시합니다.
- `/stats`: `today_kcal`은 KST 0시 이후, `total_kcal`은 전체 기록의 칼로리 합계입니다. `best_score`는 `score_version=2` 기록만 보며 없으면 0입니다.
- 시도 제한(서버 전체): `/enroll`·`/login` 합산 1분 15회, `/name-check` 1분 60회.

**코인 신뢰 한계**: 한 판에 얻은 코인 수(`coins`)는 게임 클라이언트가 보내며, 서버는 범위(0~5000)와 기록 ID 중복만 검사합니다. 조작된 클라이언트는 코인을 부풀릴 수 있습니다. 전시용으로 허용한 범위이며 실제 재화·보상에는 쓰지 않습니다. 점수·거리·칼로리도 같은 한계가 있습니다.

## 9. 로컬 Python 실행과 테스트

서버는 **PostgreSQL만** 사용합니다. 기본 방법은 위의 Docker Compose입니다. Docker 없이 Windows에서 `server.py`를 직접 실행하려면 `SetupIdentity.ps1`로 런타임(OpenCV·NumPy·Psycopg)과 모델을 준비하고, 실행 전에 `IDENTITY_DATABASE_URL`(예: `postgresql://mongscape:...@db-host:5432/mongscape`) 또는 PGHOST/PGPORT/PGDATABASE/PGUSER/PGPASSWORD_FILE을 지정합니다. 설정이 없으면 `server.py`, `migrate_account.py`, `SetupIdentity.ps1`, `StartIdentity.ps1`은 서버를 시작하지 않고 이 설정이 필요하다고 출력합니다. 로컬 실행도 Docker와 같이 관리자 권한이 없는 앱 계정을 사용합니다.

서버 테스트(`test_identity.py`, `test_http.py`)는 실제 PostgreSQL에서 실행합니다. Windows에서는 `Services/Identity` 폴더에서:

```powershell
.\SetupIdentity.ps1   # .runtime 준비. DB 설정이 없으면 런타임만 준비하고 DB 초기화 단계에서 멈춥니다.
.\run_tests.ps1       # -Python으로 Python 경로 지정 가능
```

`run_tests.ps1`은 테스트 전용 PostgreSQL 17을 임시 폴더에 `initdb`로 만들고 `127.0.0.1:55432`(`-Port`로 변경)에서 시작합니다. 비관리자 앱 계정 `mongscape`와 DB `mongscape_test`를 만들어 테스트를 실행하고, 성공·실패와 관계없이 서버를 종료하고 임시 폴더를 삭제합니다. PostgreSQL 실행 파일은 `$env:MONGSCAPE_PGSQL`(압축을 푼 `pgsql` 폴더) 또는 `.runtime/pgsql`을 사용하며, 둘 다 없으면 EDB 공식 `postgresql-17.6-1-windows-x64-binaries.zip`을 `.runtime/pgsql`에 내려받습니다. 설치형 PostgreSQL이나 실제 DB는 건드리지 않습니다.

다른 PostgreSQL에서 실행하려면 테스트 전용 DB의 앱 계정 URL을 `MONGSCAPE_TEST_POSTGRES_URL`로 지정하고 `python -m unittest -v test_identity test_http`를 실행합니다. 각 테스트는 `mongscape_test_<무작위>` 스키마를 만들고 끝나면 삭제합니다. 이 변수가 없으면 DB 테스트는 이유를 표시하고 건너뜁니다. 얼굴 모델 테스트는 공개 테스트 사진을 인터넷에서 내려받습니다.

## 확인 범위

API는 현재 단일 프로세스에서 요청을 순서대로 처리하며 얼굴 등록/로그인은 서버 전체 분당 15회, 닉네임 확인은 분당 60회 제한입니다. PostgreSQL을 도입했다고 얼굴 추론 병렬 처리까지 구현한 것은 아닙니다. 현재 계정 v2 검사 27/27과 남은 범위는 [통합 검수](../../docs/reports/PR20_LOCAL_MERGE_2026_10_29.md)를 확인하세요. `docs/reports/PR20_LOCAL_MERGE_2026_10_29.md`는 이전 서버 검증 기록입니다. 얼굴 로그인은 전시 프로필용이고 검증된 사진 위조 방지 기능은 없습니다.

공식 문서: [PostgreSQL Docker 이미지](https://hub.docker.com/_/postgres), [Psycopg 트랜잭션](https://www.psycopg.org/psycopg3/docs/basic/transactions.html), [Docker Compose](https://docs.docker.com/compose/gettingstarted/), [Caddy HTTPS](https://caddyserver.com/docs/automatic-https).
