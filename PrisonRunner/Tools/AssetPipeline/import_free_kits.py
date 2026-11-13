"""Import only vendor model/texture/license files, never third-party code."""
from pathlib import Path
import hashlib
import json
import zipfile

root = Path(__file__).resolve().parents[2]
downloads = root / "References" / "AssetDownloads"
destination = root / "Unity" / "Assets" / "External" / "Staging" / "Kenney"
record = []
manifest = root / "docs" / "FREE_ASSETS_IMPORTED.json"
previous = {item['kit']: item for item in json.loads(manifest.read_text(encoding='utf-8'))} if manifest.exists() else {}
for archive in sorted(downloads.glob("*.zip")):
    kit = archive.stem
    prior = previous.get(kit, {})
    retained = set(prior['retained_assets']) if 'retained_assets' in prior else None
    count = 0
    with zipfile.ZipFile(archive) as source:
        for entry in source.infolist():
            relative = Path(entry.filename)
            if relative.is_absolute() or ".." in relative.parts:
                raise ValueError("Invalid archive path")
            suffix = relative.suffix.lower()
            model = suffix == ".fbx"
            texture = suffix == ".png" and any("texture" in part.lower() for part in relative.parts)
            license_file = "license" in relative.name.lower() and suffix == ".txt"
            if not (model or texture or license_file):
                continue
            # The cleanup manifest keeps current game/editor inputs; licenses are always preserved.
            if retained is not None and not license_file and relative.as_posix() not in retained:
                continue
            target = destination / kit / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(source.read(entry))
            if model:
                count += 1
    item = {"kit": kit, "source": f"https://kenney.nl/assets/{kit}",
                   "license": "CC0", "models": count,
                   "archive_sha256": hashlib.sha256(archive.read_bytes()).hexdigest()}
    for key in ('original_models', 'retained_assets', 'cleanup_date'):
        if key in prior: item[key] = prior[key]
    record.append(item)
    print(kit, count, "FBX models imported")
# Do not lose entries for archives that are not installed on this machine.
record.extend(item for kit, item in previous.items() if kit not in {row['kit'] for row in record})
manifest.write_text(json.dumps(sorted(record, key=lambda item: item['kit']), indent=2), encoding="utf-8")
