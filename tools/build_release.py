"""
Builds everything a release needs into dist/:

    dist/mapmap.apworld                        the Archipelago world
    dist/MapMapArchipelago-<version>.zip       the game mod, laid out to extract straight into the game folder
    dist/Map Map - A Game About Maps.yaml      example player options

    python tools/build_release.py

Steps: check that the version number matches everywhere -> build the apworld (which runs check_tables.py first)
-> build the plugin -> zip the mod with its licences. Stops at the first failure.

The plugin build needs Map Map + BepInEx installed (it compiles against BepInEx's generated DLLs). If the game
isn't in the default Steam folder, set GAME_DIR, e.g.  set GAME_DIR=D:\\Games\\MapMap - A Game About Maps
"""
import json
import os
import re
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DIST = ROOT / "dist"
PLUGIN = ROOT / "plugin"
GAME_DIR = os.environ.get("GAME_DIR", r"C:\Program Files (x86)\Steam\steamapps\common\MapMap - A Game About Maps")


def fail(message: str) -> None:
    sys.exit(f"RELEASE FAILED: {message}")


def run(cmd: list[str]) -> None:
    print(">", " ".join(cmd))
    if subprocess.run(cmd, cwd=ROOT).returncode != 0:
        fail(f"command failed: {' '.join(cmd)}")


# --- 1. one version number everywhere -------------------------------------------------------------
versions = {
    "apworld archipelago.json": json.loads((ROOT / "apworld/mapmap/archipelago.json").read_text(encoding="utf-8"))["world_version"],
    "plugin csproj <Version>": re.search(r"<Version>(.+?)</Version>", (PLUGIN / "MapMapArchipelago.csproj").read_text(encoding="utf-8")).group(1),
    "plugin Plugin.cs Version": re.search(r'Version = "(.+?)"', (PLUGIN / "Plugin.cs").read_text(encoding="utf-8")).group(1),
}
if len(set(versions.values())) != 1:
    fail("version numbers don't match:\n  " + "\n  ".join(f"{k}: {v}" for k, v in versions.items()))
version = next(iter(versions.values()))
print(f"Version {version}")

# --- 2. apworld ---------------------------------------------------------------------------------------
run([sys.executable, str(ROOT / "tools" / "build_apworld.py")])

# --- 3. plugin ----------------------------------------------------------------------------------------
run(["dotnet", "build", str(PLUGIN), "-c", "Release", "-nologo", "-v", "q", f"-p:GameDir={GAME_DIR}"])
build_out = PLUGIN / "bin" / "Release" / "net6.0"

# --- 4. mod zip ---------------------------------------------------------------------------------------
# Paths inside the zip match the game folder, so players extract it straight into the game directory.
mod_dir = "BepInEx/plugins/MapMapArchipelago"
files = {
    f"{mod_dir}/MapMapArchipelago.dll": build_out / "MapMapArchipelago.dll",
    f"{mod_dir}/Archipelago.MultiClient.Net.dll": build_out / "Archipelago.MultiClient.Net.dll",
    f"{mod_dir}/Newtonsoft.Json.dll": build_out / "Newtonsoft.Json.dll",
    f"{mod_dir}/ap_treasure.png": PLUGIN / "assets" / "ap_treasure.png",
    f"{mod_dir}/LICENSE.txt": ROOT / "LICENSE",
}
for licence in (ROOT / "licenses").iterdir():
    files[f"{mod_dir}/licenses/{licence.name}"] = licence

missing = [str(src) for src in files.values() if not src.exists()]
if missing:
    fail("missing files:\n  " + "\n  ".join(missing))

zip_path = DIST / f"MapMapArchipelago-{version}.zip"
with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as zf:
    for name, src in files.items():
        zf.write(src, name)

shutil.copy2(ROOT / "examples" / "Map Map - A Game About Maps.yaml", DIST)

print("\nRelease files in dist/:")
for path in sorted(DIST.iterdir()):
    print(f"  {path.name:45} {path.stat().st_size:>9,} bytes")
