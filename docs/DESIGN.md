# Map Map – Archipelago Design (draft v0.1)

Status: **draft for review**. Nothing here is code yet. Items marked **[DECIDE]** need an answer from the project owner.

## Decisions log
| # | Decision | Date |
|---|---|---|
| 1 | Connection: **option A**, the plugin connects itself (Archipelago.MultiClient.Net plus an in-game connect panel on the main menu) | 2026-09-30 |
| 2 | Starting islands: **Island 1 only** (a small sphere 1 is fine) | 2026-09-30 |
| 3 | Tools: **Build Tool is hard-required** on some swamp islands (bridges; which ones TBD). **Camera is needed for exactly 1 mission** (TBD). Other tool tutorials are doable without the tool, so those rules are soft only. **Glide is a new progression item**, needed for some Floor Is Lava on the last 4–5 islands (which ones TBD). | 2026-09-30 |
| 4 | `Mission_Compass4_4`: unknown. Default it to **excluded** (filler only) until identified at runtime. | 2026-09-30 |
| 5 | Goal: **option** with three choices: Finish Island 20 (default), Complete N islands, All 3-star ratings | 2026-09-30 |
| 6 | Goal trigger for "Finish Island 20" = completing `Mission_Compass4_4` "Enter the Raven King's Treasure Cave" (it triggers the final cutscene and credits). It's an event, not a location. | 2026-09-30 |
| 7 | Camera: needed for the "take a picture of the map" **story step** on Island 10 (not a mission object, not star-able). Island 10 missions from set 2 (index 1) onward require Camera in logic, because without it the story likely can't continue. Verify in testing. | 2026-09-30 |
| 8 | "Place the island piece" (Islands 14, 15, 17, 20) and "Place the following house" (Island 18) need no tool; they're silhouette markers. | 2026-09-30 |
| 10 | Connect panel fields: **Server, Slot Name, Password** (+ "Remember password" toggle, off by default) and a Connect button + status line. Server and Slot Name are remembered in the BepInEx config. The probe test (2026-09-30) proved the home-made field (typing, Backspace, Enter, Ctrl+V paste, masking) and 1080p-relative scaling work in-game. | 2026-09-30 |
| 11 | **Build Tool** is required for every check on the swamp islands, **Islands 12–16** (scene names `Island_12_Swamp1` … `Island_16_Swamp5_Holz`). **Island 17 does NOT need it** (owner confirmed). | 2026-09-30 |
| 13 | **Star checks:** use the game's own `StarRating.GetStarRating(float distance)` (the island panel calls it; cutoffs are serialized scene data copied to statics in `StarRating.Start`). Send the 3-star check when the best distance is **>= 0** and GetStarRating returns 3. **Never trust the panel display**: a never-played mission has distance **-1.0** and the panel shows 3 stars for it (seen on Islands 13–17 after the forward jump). Samples: 0.0–0.5 → 3 stars, 3.0 → 1 star. | 2026-09-30 |
| 12 | **Glide** is required for every Floor Is Lava on **Islands 14–20** (16 of 18; Island 13's 2 need nothing). The source is `LevelSequence14.OnDialogueFinished`: after set 0 / dialogue 1 on Island 14, in Progression mode, it sends `ActivateGliderMessage(true)` (also in `UnlockPartOfSequence(set>=1)` and `UnlockAllForSequence`). The plugin must block those so Glide only comes from AP. | 2026-09-30 |
| 14 | Tool logic: Dividers, Telescope and Compass are helpful but **never required**. Option `helpful_tool_logic` (default ON) puts **Dividers → Islands 7–20** and **Telescope → Islands 17–20** into logic. **Compass is classified useful and never in logic** (helpful on every map; gating every island on it would make it a de-facto starting item). Sextant is also useful-only. | 2026-09-30 |
| 15 | Options: `goal` (island_20 / islands_completed / all_three_stars), `islands_required` (1–20, default 10; "complete" = every mission on that island), `star_checks`, `treasure_checks`, `floor_is_lava_checks` (all default ON; FIL added at the owner's request because late ones are tough), `helpful_tool_logic`. Filler name **"Ink Blot"** confirmed. | 2026-09-30 |
| 16 | Generation fix: with `star_checks` off, Island 1 has a single starting check, and 2 of 8 solo test seeds failed (`FillError: No more spots`). Fix: **solo seeds only** with star checks off pre-fill "Island 1 - Mission 1" with a random tool-free island. **Multiworld seeds are never pre-filled** (owner decision: no guaranteed progression in sphere 1 in multiworlds); other games' starting checks give the fill room. Star checks on gives 2 starting checks (16/16 seeds OK, no fix needed). | 2026-10-01 |
| 17 | Naming: game name **"Map Map - A Game About Maps"** (YAML `game:`, server, trackers, docs); apworld file/module **`mapmap`** (`mapmap.apworld`). Same split as tloz_oos. | 2026-10-01 |
| 18 | Checks (owner decision): treasures give **only the AP check**, not their vanilla sticker/pencil (blocked while `TreasureUI.DisplayUI` runs). **Only progress made live in the AP save counts**: checks are detected from game events, never by scanning the save, so earlier progress is never sent. | 2026-10-02 |
| 9 | Connect panel: a home-made IMGUI text field (GUI.TextField is stripped). It reads keys **only from Unity's own input events, only while the field is focused**, with no global keyboard hooks. The real mod must not use `GetAsyncKeyState` (that's probe-only). "Remember password" is an option, off by default (config is plain text). | 2026-09-30 |
Target: Archipelago 0.6.7, BepInEx 6 (IL2CPP) be.788, Map Map build with Unity 2022.3.62f3.

---

## 1. How the pieces fit together

```
 Archipelago server  <-->  [connection layer]  <-->  BepInEx plugin (inside Map Map)
                                                       |- tells the server when you complete a check
                                                       |- receives items (island unlocks, tools)
                                                       '- changes game behaviour (world map, travel, tools)
 Generation:  YAML options --> apworld (Python) --> seed with items placed into locations
```

* **apworld** (Python): describes the items, locations, regions/logic, options and goal. It runs only during generation and on the server.
* **plugin** (C#): runs inside the game. It does everything the probe proved is possible.
* **Connection layer [DECIDE]**, two options:
  * **A. In-plugin client:** the plugin talks to the AP server directly using *Archipelago.MultiClient.Net*, the standard C# AP library. Players just type the server address into a config file or an in-game box. There's no separate client window, and this is the usual approach for BepInEx AP games.
  * **B. Bridge files + Python client:** same architecture as WitchSpring R. The plugin writes and reads JSON files, and a Python client in the AP Launcher relays them to the server. You already know how to support this, but it's more moving parts for players.
  * **DECIDED: A.** Player flow: a connect panel on the main menu (Server / Slot / Password / Connect, remembered in the BepInEx config) and a status indicator in-game; hints and chat go through the normal AP Text Client. Received islands and tools are idempotent unlocks, so re-sending the full item list on reconnect is harmless. Checks made while offline are queued in `ap_state.json` and sent on reconnect. Network callbacks arrive on a background thread and are applied on Unity's main thread through a queue. IMGUI test (probe v0.2): Box, Label, Toggle, Button and GUILayout work, but **GUI.TextField is stripped from this game build** (`Method unstripping failed`). The connect panel therefore needs a home-made text field (read keystrokes from `Event.current` in OnGUI), or a uGUI/TextMeshPro input field built at runtime. It also needs scaling, because it renders tiny at high resolutions.

---

## 2. Facts this design relies on (from the probe, 2026-09-30)

| Fact | How we know |
|---|---|
| 20 islands; island ID = campaign position, counting from 0 (Island 16 = ID 15) | Probe dump: `allIslands`, `missionUnits`, `WorldMapIsland.islandID` all agree |
| Any island can be played in **story mode** out of order (dialogue and chained missions) | Test 3: set the story pointer, then call `LoadCurrentMission` |
| Treasure, landmark and Floor Is Lava pickups report on any island, in any mode | Test 1 log |
| The save holds only **one** story position; the map unlocks everything before it | Test 3: reload put you back on Island 18, and 13–17 appeared |
| The game sends "mission finished" on **every marker attempt**, including misses | Test 3 log: `Compass1_2_1` "finished" 4 times |
| Stars come from the distance result; all sampled results of 0.0–0.5 gave 3 stars | Test 3 `[stars]` lines; exact cutoffs still unknown |

---

## 3. Items

### 3.1 Island unlocks (progression), 20 total
`Island 1` … `Island 20`. Receiving one makes that island show up on the world map and lets you travel there.

* **Starting island (DECIDED):** only **Island 1** starts unlocked; Islands 2–20 are shuffled. Sphere 1 is small (Island 1's mission + its 3-star check), and that's accepted.

### 3.2 Tools
From the probe dump, this is where the game normally hands each tool out:

| Tool (game name) | Normally given on | Proposal |
|---|---|---|
| Drawing, CountSteps, Grid, ScaleReference, CompassRose | Islands 1 & 3 | **Always given at start.** These are basic map-making UI; randomizing them would make early missions miserable. |
| **Shovel** | Island 4 (set 3) | **Progression.** Hard-required for every buried treasure and every treasure mission. |
| **Dividers** (+ DividerDistanceText) | Island 6 (dividers tutorial); DistanceText on Island 16 | **Progression** (one item; the distance readout comes with it) |
| **Camera** | Island 10 | **Progression**: needed for exactly 1 mission (which one TBD, see 3.4) |
| **Build Tool** | Island 12 | **Progression**: hard-required on some swamp islands for bridges (which ones TBD, see 3.4) |
| **Glide** (ability, not a tool) | Island 14 (`LevelSequence14` glider tutorial; turned on via `ActivateGliderMessage`, saved as `glidingEnabled`) | **Progression**: needed for some Floor Is Lava on the last 4–5 islands (which ones TBD) |
| **Telescope** | Island 16 | Progression (soft logic, see 4.2) |
| **Compass** | Island 18 (compass tutorial) | Progression |
| **Sextant** | Island 19 (set 4) | Progression |

### 3.4 Finding which checks need Camera, Build Tool or Glide
* ~~`toolsForTesting` may identify tool needs~~: the probe found it **empty for every mission**, so there's no data shortcut. Tool requirements come from the owner's knowledge or playtesting. The probe dump lists every mission's on-screen text to help with that.
* Glide-only Floor Is Lava: no data flag is known. Identify them by playtesting the last 4–5 islands without glide, or from owner recall.

The plugin must stop the game from handing these out by itself (the game's `UnlockToolsForMissionUnit`). Otherwise the item would be free and the logic wouldn't matter.

### 3.3 Filler
Treasures normally give stickers or pencils (cosmetic). If treasures become checks, their stickers and pencils go into the item pool as filler. The runtime count is **43 stickers + 6 pencils = 49 cosmetic items**. Any remaining slots get a generic filler item, for example "Ink Blot" (it does nothing), or optional traps later.

Count (from `tools/check_tables.py`): 19 shuffled islands + 8 tools/abilities = **27 progression/useful items**, 49 stickers/pencils and 123 "Ink Blot" generic filler, against **199 locations**. The pool is mostly filler, which is normal for a game with lots of checks.

---

## 4. Locations

### 4.1 Types and counts (from the probe dump)

| Type | Count | Name pattern | Default |
|---|---|---|---|
| Mission complete | 69 | `Island 16 - Mission 1.3` | always on |
| 3-star mission | ~61 (not treasure missions) | `Island 16 - Mission 1.3 (3 Stars)` | on (option `star_checks`) |
| Treasure dug up | 40 | `Island 16 - Treasure 1` | on (option `treasuresanity`) |
| Floor Is Lava | 18 | `Island 16 - Floor Is Lava 1` | on |
| Landmark | 12 | `Island 16 - Landmark` | on |

Mission numbering follows the game's own island panel ("1", "2.1", "2.2", …) so players can match names to what's on screen.

* **`Mission_Compass4_4`** (Island 20, final set) is flagged `excludeFromCount`. Its text is **"Enter the Raven King's Treasure Cave"**, which looks like the ending trigger. Proposal: make it the **goal event** for goal 1 rather than a location. (Confirm with the owner.)
* **Completing a mission** = the game marks it complete (`isCompleted`). It is *not* the "finished" message, which fires on misses too.

### 4.2 Logic (what each check needs)

```
Menu --> World Map --> Island N   (needs "Island N")
                        |- missions, Floor Is Lava, landmarks: need only the island (+ tool rules below)
                        '- treasures and treasure missions:   also need "Shovel"
```

Tool rules (from your notes: almost nothing *strictly* needs a tool, but later islands get tool requirements to keep things fair):
* **Hard:** treasures and treasure missions need Shovel; bridge-dependent swamp checks need Build Tool; Island 10 missions in set 2+ need Camera (decision 7); glide-only Floor Is Lava need Glide. The other tool tutorials (Dividers, Compass, Sextant) are doable without their tool (owner confirmed).
* **Soft (fairness, still in logic):** Islands 7–20 need Dividers; Islands 17–20 need Telescope; Islands 19–20 need Compass. **[DECIDE]** Final soft table after the owner reviews.

### 4.3 Location ID scheme (readable on purpose)
`id = BASE + island * 1000 + type * 100 + index`
* type: 0 = mission, 1 = 3-star, 2 = treasure, 3 = Floor Is Lava, 4 = landmark
* Example: Island 16, 2nd treasure → `BASE + 16000 + 200 + 2`. That makes a location ID easy to decode when debugging a support report.
* Items: `BASE + 1..20` for islands, `BASE + 100+` for tools, `BASE + 500+` for filler.

---

## 5. Goal (DECIDED: an option)
`goal` option:
1. **Finish Island 20** (default): the vanilla ending (final mission or outro). Needs Island 20, so the whole progression chain is involved.
2. **Complete N islands**: all missions on N islands; `islands_required` is configurable.
3. **All 3-star ratings** (long); only valid with `star_checks` on.

Note for goal 1: the only logic requirement is Island 20 plus whatever its own checks need. Players could rush it, which is normal for AP.

---

## 6. What the plugin has to do (summary; details at build time)

| Job | How (game method, proven or likely) |
|---|---|
| Track each island's progress | Own state file `ap_state.json` per seed/slot: which set each island is on and which checks were sent |
| Travel to an island in story mode | Set `progressMissionUnit`, `currentMissionUnit` and `missionUnits[n].currentMissionSet`, then call `LoadCurrentMission` (proven in test 3) |
| Island finished → back to the map, not the next island | Intercept `OnMissionUnitFinished` / `NextMissionUnit` |
| World map shows only AP-unlocked islands | After `UnlockAllPrevious` / map load, set `WorldMapIsland.isUnlocked` from received items |
| Tools come only from AP | Block `UnlockToolsForMissionUnit` and grant tools when items arrive |
| Send checks | Mission complete (`isCompleted`), stars (from distance), `OnUnlockTreasure`, `OnUnlockLandmark`, `OnCompleteFloorIsLava` |
| Keep a seed from touching your normal save | **[DECIDE]** Use a dedicated save slot (the game has `savegame00`; check whether there are more slots) |

Open technical unknowns (none block the design):
* Star cutoffs (need a sample with fewer than 3 stars, or find them in the code)
* How tool tutorials behave when the tool arrives before or after its island
* Sticker and pencil counts for filler
* Whether there's more than one save slot

---

## 7. Publishing checklist (AP Developer Code of Conduct)
* Public repo; release files built from the repo contents
* LICENSE file (MIT is common for apworlds)
* Credits: Archipelago, BepInEx, Il2CppInterop, HarmonyX, Archipelago.MultiClient.Net (if option A), Cpp2IL (used for research only)
* No decompiled game code or game assets in the repo (the plugin only references the game's interop DLLs at build time)
* README states that AI assistance was used and that the owner reviewed and tested the code
