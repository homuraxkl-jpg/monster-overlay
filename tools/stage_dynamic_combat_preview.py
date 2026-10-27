"""Stage only the three approved upgrade files; never copy runtime configuration.

Does not build, install, modify baseline hashes, or package a full public release.
"""
from pathlib import Path
import hashlib
import json
import shutil

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / "dist/DynamicCombatPreview"
FILES = {
    "WhaleCompanion.dll": ROOT / "src/WhaleCompanion/bin/Release/net8.0/WhaleCompanion.dll",
    "P11-5-DYNAMIC-COMBAT.md": ROOT / "P11-5-DYNAMIC-COMBAT.md",
    "THIRD-PARTY.md": ROOT / "THIRD-PARTY.md",
}
TARGET_PREFIX = "plugins/CSharp/WhaleCompanion/"

def stage():
    manifest = STAGE / "upgrade-manifest.json"
    rows = json.loads(manifest.read_text(encoding="utf-8-sig"))
    # Validate everything before writing. Do not accept arbitrary manifest paths.
    if len(rows) != len(FILES) or {row["Source"] for row in rows} != set(FILES):
        raise ValueError("Upgrade manifest must contain exactly the approved three files")
    for row in rows:
        name = row["Source"]
        if row["Target"] != TARGET_PREFIX + name or row.get("Preserve") is not False:
            raise ValueError("Unexpected upgrade target or preserve policy")
        if not FILES[name].is_file() or FILES[name].stat().st_size == 0:
            raise ValueError("Build/source missing: " + name)
    for row in rows:
        source = FILES[row["Source"]]
        target = STAGE / row["Source"]
        shutil.copyfile(source, target)
        row["NewHash"] = hashlib.sha256(target.read_bytes()).hexdigest().upper()
        if row["NewHash"] != hashlib.sha256(source.read_bytes()).hexdigest().upper():
            raise ValueError("Staged hash mismatch")
    # Atomic manifest replacement; ExpectedHash and original baseline are retained.
    pending = manifest.with_suffix(".json.tmp")
    pending.write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding="utf-8-sig")
    pending.replace(manifest)
    print("Staged 3 approved files; configuration and baseline hashes retained.")

if __name__ == "__main__":
    stage()
