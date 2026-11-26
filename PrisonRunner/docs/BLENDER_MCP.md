# Blender MCP 연결 (오너 작업 안내)

Claude가 Blender를 직접 다뤄 에셋을 만들 수 있게 하는 설정이다. 저장소 루트의 `.mcp.json`이 MCP 서버 `blender`(PyPI `mcp-for-blender` 2.3.1, `uvx`로 실행)를 등록해 두었다. 오너는 Blender 쪽 애드온만 한 번 켜면 된다.

- Blender: `M:\Blender\blender.exe` (5.3). 2026-11-26에 이 버전으로 애드온 로드, 장면 조회, 코드 실행, 오류 뒤 재연결까지 확인했다.
- 애드온: [`Tools/BlenderMCP/addon.py`](../Tools/BlenderMCP/addon.py) (MIT, 출처는 같은 폴더 README)
- `uv`는 2026-11-26에 winget(`astral-sh.uv`)으로 설치했다. 설치 전에 열어 둔 터미널·Orca에서는 `uvx`를 못 찾으니 새로 연다.

## 1. Blender에 애드온 설치 (한 번만)

1. Blender를 연다.
2. **Edit → Preferences → Add-ons** → 오른쪽 위 **▾** → **Install from Disk…**
3. 저장소의 `PrisonRunner/Tools/BlenderMCP/addon.py`를 고른다.
4. 목록의 **Interface: MCP for Blender**에 체크한다.

`uvx mcp-for-blender install-addon` 명령은 쓰지 않는다. Blender 5.3 확장 방식 설치에서 사이드바 오류 보고(업스트림 이슈 #443)가 있다.

## 2. 사이드바에서 서버 켜기

1. 3D 뷰포트에서 **N**을 눌러 사이드바를 연다.
2. **MCP for Blender** 탭을 연다.
3. 서버는 기본으로 자동 시작한다(`localhost:9876`). 꺼져 있으면 **Connect to MCP server**를 누른다.

Meshy, Tripo, World Labs, Poly Haven 같은 외부 생성 기능은 켜지 말고 API 키도 넣지 않는다. 에셋은 Blender 안의 모델링으로 만든다.

## 3. Claude 세션에서 연결 확인

1. 저장소 루트(또는 그 워크트리)에서 Claude Code를 새로 연다.
2. 처음에 `.mcp.json`의 `blender` 서버를 허용할지 물으면 허용한다.
3. `/mcp`에 `blender`가 connected로 보이는지 본다.
4. "Blender 장면 정보 알려줘"라고 시켜 본다. `get_scene_info`가 오브젝트 목록을 돌려주면 끝이다.

## 주의

- **한 번에 한 Claude 세션만 연결한다.** Blender 애드온은 포트 9876 하나로 명령을 받는다. 여러 세션(Orca 워커 여럿)이 동시에 붙으면 명령이 섞이거나 응답이 막힌다. Blender 작업은 한 세션에만 맡기고, 다른 세션은 `/mcp`에서 `blender`를 끄거나 처음 물을 때 거절한다.
- 연결된 세션은 Blender에서 임의의 Python 코드를 실행할 수 있다. 소켓에 인증이 없으니 localhost에서만 쓰고, 일이 끝나면 사이드바에서 서버를 끈다.
- 원격 사용 통계는 `.mcp.json`의 `DISABLE_TELEMETRY=true`로 껐다. 애드온 설정의 telemetry consent도 켜지 않는다.
- 애드온과 서버 버전은 함께 올린다. `addon.py`의 `ADDON_PROTOCOL_VERSION`(현재 16)이 서버 패키지와 맞아야 한다.
