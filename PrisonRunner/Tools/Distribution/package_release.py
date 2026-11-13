"""Create the Windows release ZIP (release build only) without local caches, secrets or raw Mixamo."""
from pathlib import Path
import hashlib
import json
import zipfile

PROJECT = Path(__file__).resolve().parents[2]
OUTPUT = PROJECT.parent / "Distribution"


def package():
    OUTPUT.mkdir(exist_ok=True)
    build = PROJECT / "Builds/Mongscape/Windows"
    for required in ("Mongscape.exe", "UnityPlayer.dll", "Mongscape_Data/boot.config", "MonoBleedingEdge"):
        if not (build / required).exists():
            raise RuntimeError(f"Required game component missing: {required}")
    marker = build / "Mongscape.complete"
    flavor = marker.read_text(encoding="utf-8").split() if marker.is_file() else []
    if not flavor or flavor[0] != "release":
        raise RuntimeError("Release build required: run Mongscape > Build Windows Release (Dev/QA builds are not shipped)")
    privacy = PROJECT / "docs/PRIVACY_POLICY.md"
    if "[작성 필요" in privacy.read_text(encoding="utf-8"):
        raise RuntimeError("docs/PRIVACY_POLICY.md still has [작성 필요] placeholders; fill them in before shipping")
    entries = {}
    for path in build.rglob("*"):
        if not path.is_file() or any("DoNotShip" in part for part in path.parts):
            continue
        if path.suffix.lower() in (".pdb", ".log"):
            continue
        entries[f"Game/{path.relative_to(build).as_posix()}"] = path
    # No file may carry this PC's folder path (e.g. a DLL's debug-info path); the release build maps it away.
    local = {str(PROJECT), PROJECT.as_posix()}
    needles = [n.encode(enc) for n in local for enc in ("utf-8", "utf-16-le")]
    leaks = [name for name, path in entries.items() if any(n in path.read_bytes() for n in needles)]
    if leaks:
        raise RuntimeError("Build PC folder path found in: " + ", ".join(leaks[:5]) + " (rebuild with Mongscape > Build Windows Release)")
    entries["THIRD_PARTY_NOTICES.md"] = PROJECT / "THIRD_PARTY_NOTICES.md"
    entries["PRIVACY_POLICY.md"] = privacy
    entries["CHANGELOG.md"] = PROJECT / "CHANGELOG.md"
    entries["SOURCES/BlackHanSans/OFL.txt"] = PROJECT / "Unity/Assets/Game/Content/UI/Resources/MuhanokUI/BlackHanSans-OFL.txt"
    entries["docs/FREE_ASSETS_IMPORTED.json"] = PROJECT / "docs/FREE_ASSETS_IMPORTED.json"
    entries["References/MixamoDownloads/Provenance.json"] = PROJECT / "References/MixamoDownloads/Provenance.json"
    for path in (PROJECT / "Unity/Assets/External/Staging/Kenney").rglob("*"):
        if path.is_file() and path.name.lower().startswith("license") and path.suffix != ".meta":
            entries[f"SOURCES/Kenney/{path.parent.name}/{path.name}"] = path
    launcher = '@echo off\r\nsetlocal\r\nstart "" /d "%~dp0Game" "%~dp0Game\\Mongscape.exe" %*\r\n'
    start_guide = """# Mongscape 실행 안내

1. ZIP 전체를 압축 해제합니다. `Game` 폴더 전체를 그대로 두세요.
2. `StartGame.cmd`를 더블클릭합니다. 실제 게임은 `Game/Mongscape.exe`입니다. "Windows의 PC 보호" 창이 뜨면 **추가 정보 → 실행**을 누르세요(실행 파일에 코드 서명이 없어서 뜨는 안내입니다).
3. **얼굴 로그인 / 회원가입 / 게스트 플레이** 중 하나를 고릅니다. 가입 없이 바로 하려면 게스트 플레이를 고르세요.
4. 메인 메뉴의 **게임 시작**에서 플레이 방식(전신 / 상체 / 키보드)을 고르고 **시작하기**를 누릅니다.

키보드: A/D 또는 좌우 화살표는 이동(수레에서는 몸 기울이기), C는 갈림길 가운데, Space는 점프, S 또는 ↓는 숙이기, W 또는 ↑는 하이니입니다. F1은 설정입니다.

웹캠 몸동작은 게임 안에서 처리하며 영상을 저장하거나 전송하지 않습니다. 카메라 미리보기에 몸이 보이게 서세요. Unity·Python 설치는 필요 없습니다.

회원가입은 만 14세 이상만 할 수 있고, 얼굴 특징값(사진이 아닌 숫자)을 별도로 동의받아 저장합니다. 자세한 내용은 `PRIVACY_POLICY.md`, 변경 내용은 `CHANGELOG.md`, 외부 리소스와 라이선스는 `THIRD_PARTY_NOTICES.md`를 확인하세요.
"""
    manifest = {name: hashlib.sha256(path.read_bytes()).hexdigest() for name, path in sorted(entries.items())}
    manifest["StartGame.cmd"] = hashlib.sha256(launcher.encode("ascii")).hexdigest()
    manifest["START_HERE.md"] = hashlib.sha256(start_guide.encode("utf-8")).hexdigest()
    readme = "Mongscape Windows 실행용\r\n\r\nZIP 전체를 압축 해제하고 StartGame.cmd를 더블클릭하세요.\r\n게스트 플레이를 고르면 가입 없이 바로 할 수 있습니다.\r\n키보드: A/D 이동, Space 점프, S 숙이기, W 하이니.\r\n실행에 필요한 Game 폴더 전체를 유지하세요.\r\n자세한 안내는 START_HERE.md, 개인정보는 PRIVACY_POLICY.md를 확인하세요.\r\n"
    with zipfile.ZipFile(OUTPUT / "Mongscape-Windows-x64.zip", "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for name, path in sorted(entries.items()):
            archive.write(path, name)
        archive.writestr("StartGame.cmd", launcher)
        archive.writestr("START_HERE.md", start_guide)
        archive.writestr("처음읽기.txt", readme.encode("utf-8-sig"))
        archive.writestr("SHA256.json", json.dumps(manifest, indent=2))
    zips = [OUTPUT / "Mongscape-Windows-x64.zip"]
    for path in zips:
        with zipfile.ZipFile(path) as archive:
            if archive.testzip() is not None:
                raise RuntimeError(f"ZIP integrity failed: {path.name}")
            # No local environments, debug folders, raw Mixamo, databases, secrets or face photos.
            blocked = (".venv/", "DoNotShip", ".env", "PlayerData/")
            if any(any(b in name for b in blocked) or name.lower().endswith((".fbx", ".db", ".dump", ".sqlite", ".jpg", ".jpeg")) for name in archive.namelist()):
                raise RuntimeError(f"Unexpected distribution file: {path.name}")
        print(f"{path.name}: {path.stat().st_size} bytes, SHA256 {hashlib.sha256(path.read_bytes()).hexdigest()}")
    hashes = "\n".join(f"{hashlib.sha256(path.read_bytes()).hexdigest()}  {path.name}" for path in zips) + "\n"
    (OUTPUT / "SHA256SUMS.txt").write_text(hashes, encoding="ascii")


if __name__ == "__main__":
    package()
