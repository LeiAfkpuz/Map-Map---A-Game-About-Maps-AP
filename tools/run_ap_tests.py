"""
Runs Map Map's tests (and optionally Archipelago's general test suite) against an Archipelago
source checkout.

    python tools/run_ap_tests.py            # Map Map's own tests (fast)
    python tools/run_ap_tests.py --general  # also Archipelago's general suite (slow: tests every world)

One-time setup (already done on the dev machine, 2026-10-01):
    git clone --depth 1 --branch 0.6.7 https://github.com/ArchipelagoMW/Archipelago.git Archipelago-0.6.7-src
    cd Archipelago-0.6.7-src && python -m venv .venv
    .venv/Scripts/python -m pip install -r requirements.txt pytest   (kivy/kivymd can be skipped)

The apworld folder is copied to <AP>/worlds/mapmap each run, so the checkout always tests the
current code. Nothing in the AP checkout itself is modified apart from that folder.
"""
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "apworld" / "mapmap"
AP = ROOT.parent.parent / "Archipelago-0.6.7-src"   # Documents/Archipelago Stuff/Archipelago-0.6.7-src
AP_PYTHON = AP / ".venv" / "Scripts" / "python.exe"

if not AP_PYTHON.exists():
    sys.exit(f"Archipelago test checkout not found at {AP} (see setup notes at the top of this file).")

target = AP / "worlds" / "mapmap"
shutil.rmtree(target, ignore_errors=True)
shutil.copytree(SRC, target, ignore=shutil.ignore_patterns("__pycache__"))

suites = ["worlds/mapmap/test"] + (["test/general"] if "--general" in sys.argv else [])
result = subprocess.run([str(AP_PYTHON), "-m", "pytest", "-q", "-p", "no:cacheprovider", *suites], cwd=AP)
sys.exit(result.returncode)
