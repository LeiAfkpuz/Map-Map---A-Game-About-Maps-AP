# Building from source

## Requirements

- Python 3.11–3.13 (for the apworld tools)
- .NET SDK 6.0 or newer (the plugin targets `net6.0`; developed with SDK 10)
- Map Map installed with BepInEx 6 IL2CPP, **run once** so BepInEx has generated `BepInEx\interop\` — the plugin
  compiles against those generated DLLs and BepInEx's `core` DLLs (neither is in this repository).

## Everyday commands

```
python tools/check_tables.py            # validate item/location tables + the ID snapshot
python tools/check_tables.py --list     # print every location with its ID
python tools/build_apworld.py --install # build dist/mapmap.apworld and copy it into Archipelago's custom_worlds
dotnet build plugin -c Release          # build the plugin and copy it into the game's BepInEx\plugins folder
python tools/build_release.py           # everything above + the release zip, into dist/
python tools/run_ap_tests.py            # Map Map's unit tests (needs an Archipelago source checkout, see the file)
```

If the game isn't in the default Steam folder, pass its location to the plugin build:
`dotnet build plugin -c Release -p:GameDir="D:\Games\MapMap - A Game About Maps"`
(and set `GAME_DIR` the same way for `build_release.py`, see the top of that file).

## The ID snapshot

`tools/id_snapshot.json` records every item and location ID. IDs must never change after a release (servers,
trackers and in-progress seeds rely on them). `check_tables.py` fails if an existing ID changes or disappears; new
entries must be accepted with `--update-snapshot` after reviewing them.

## Where things are explained

- `docs/DESIGN.md` — the design and the numbered decisions log (why each rule and option is the way it is).
- Each plugin source file starts with a comment describing what it does and why; patches name the game method they
  hook and what testing found.
