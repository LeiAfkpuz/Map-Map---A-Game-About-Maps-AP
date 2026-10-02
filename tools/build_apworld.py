"""
Packages apworld/mapmap/ into mapmap.apworld (a zip whose root contains the "mapmap" folder).

    python tools/build_apworld.py             # writes dist/mapmap.apworld
    python tools/build_apworld.py --install   # also copies it into Archipelago's custom_worlds

Runs check_tables.py first and refuses to build if it fails.
"""
import json
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "apworld" / "mapmap"
DIST = ROOT / "dist"
OUT = DIST / "mapmap.apworld"
CUSTOM_WORLDS = Path(r"C:\ProgramData\Archipelago\custom_worlds")

check = subprocess.run([sys.executable, str(ROOT / "tools" / "check_tables.py")])
if check.returncode != 0:
    sys.exit("check_tables.py failed - not building.")

# The manifest: the source archipelago.json (game, world_version, minimum_ap_version, authors) plus the
# container fields Archipelago's own packaging adds (worlds/Files.py, APWorldContainer, AP 0.6.7). Without
# "compatible_version" the loader rejects the manifest and shows the world as v0.0.0 (found 2026-10-02).
manifest = json.loads((SRC / "archipelago.json").read_text(encoding="utf-8"))
manifest.update({"compatible_version": 7, "version": 7})

DIST.mkdir(exist_ok=True)
with zipfile.ZipFile(OUT, "w", zipfile.ZIP_DEFLATED) as zf:
    for path in sorted(SRC.rglob("*")):
        if "__pycache__" in path.parts or path.is_dir() or path.name == "archipelago.json":
            continue
        zf.write(path, Path("mapmap") / path.relative_to(SRC))
    zf.writestr("mapmap/archipelago.json", json.dumps(manifest, indent=1))
print(f"Built {OUT} ({OUT.stat().st_size} bytes)")

if "--install" in sys.argv:
    shutil.copy2(OUT, CUSTOM_WORLDS / OUT.name)
    print(f"Installed to {CUSTOM_WORLDS / OUT.name}")
