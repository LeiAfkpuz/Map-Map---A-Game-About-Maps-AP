# Map Map - A Game About Maps — Archipelago

An [Archipelago](https://archipelago.gg) multiworld randomizer for
[Map Map - A Game About Maps](https://store.steampowered.com/app/2702260/).

Play the 20 islands in any order: islands, tools and abilities are items from the multiworld, and missions, 3-star
ratings, buried treasures, Floor Is Lava challenges and landmarks are checks.

**Status: 0.1.0 — early testing release.** Expect rough edges; please report anything odd (see *Reporting bugs*).

> **AI disclosure:** the code in this project was written almost entirely by an AI (Claude), directed, reviewed and
> tested in-game by myself. Full details of what the AI did and didn't do: [docs/AI_DISCLOSURE.md](docs/AI_DISCLOSURE.md).

## Download & setup

Grab the latest release from this repository's **Releases** page:

- `MapMapArchipelago-<version>.zip` — the game mod (extract into the game folder)
- `mapmap.apworld` — the Archipelago world (goes in Archipelago's `custom_worlds` folder)
- `Map Map - A Game About Maps.yaml` — an example player options file

Full instructions: [setup guide](apworld/mapmap/docs/setup_en.md). Game info, items, checks and goals:
[game page](apworld/mapmap/docs/en_Map%20Map%20-%20A%20Game%20About%20Maps.md).

Requires Archipelago 0.6.7+ and BepInEx 6 (bleeding edge, Unity IL2CPP x64; tested with build 788).

## Options

| Option | Default | |
|---|---|---|
| `goal` | `island_20` | `island_20`, `islands_completed` or `all_three_stars` |
| `islands_required` | 10 | islands to fully complete for the `islands_completed` goal |
| `star_checks` | on | 3-star rating on each mission (61 checks) |
| `treasure_checks` | on | each buried treasure (40 checks, needs the Shovel) |
| `floor_is_lava_checks` | on | each Floor Is Lava challenge (18 checks) |
| `helpful_tool_logic` | on | Dividers (Islands 7–20) and Telescope (Islands 17–20) in logic |

## Known issues (0.1.0)

- Returning to an island partway through a mission set replays that set's opening dialogue (your finished missions
  are kept; just click through).
- The BepInEx log shows one `NullReferenceException ... native->managed trampoline` when an island loads. It comes
  from the game's own tool-unlock code, is caught, and is harmless.
- The `all_three_stars` goal passes the automated tests but hasn't been played to completion in-game yet. The
  `island_20` and `islands_completed` goals have.
- An island's own story may still announce a tool (e.g. "New Tool") that Archipelago hasn't given you yet; the tool
  itself stays locked until you receive it.

## Reporting bugs

Open an issue with what happened, what you expected, and your `BepInEx\LogOutput.log` from the game folder.

## Repository layout

| Folder | What's in it |
|---|---|
| `apworld/mapmap/` | The Archipelago world (Python): items, locations, logic, options, tests, docs |
| `plugin/` | The game mod (C#, BepInEx 6 IL2CPP + Harmony) |
| `tools/` | Developer scripts: table checks + ID snapshot, apworld/release builders, test runner |
| `docs/` | Design notes and the decisions log |
| `licenses/` | Licences of the third-party libraries shipped with the mod |

Building from source: see [docs/BUILDING.md](docs/BUILDING.md).

## Credits & licences

- Created and maintained by **LeiAfkpuz**. Licensed under the [MIT License](LICENSE).
- Code, research and documentation drafts written almost entirely by **Claude (Anthropic's AI)** under the
  maintainer's direction; design decisions, review and all in-game testing by the maintainer, who is responsible for
  it. See [docs/AI_DISCLOSURE.md](docs/AI_DISCLOSURE.md).
- [Archipelago](https://github.com/ArchipelagoMW/Archipelago) (MIT) — the multiworld framework.
- [BepInEx](https://github.com/BepInEx/BepInEx) (LGPL-2.1), [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop)
  (LGPL-3.0) and [HarmonyX](https://github.com/BepInEx/HarmonyX) (MIT) — mod loader and patching; installed separately
  by players, not included in this repository or the release.
- Shipped with the mod (licence texts in [`licenses/`](licenses/)):
  [Archipelago.MultiClient.Net](https://github.com/ArchipelagoMW/Archipelago.MultiClient.Net) (MIT) and
  [Newtonsoft.Json](https://www.newtonsoft.com/json) (MIT).
- [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL) was used during research to understand the game's structure.
  No game code or assets are included in this repository.

*Map Map - A Game About Maps* belongs to its developers and publisher. This is an unofficial fan project.
