# AI Usage Disclosure — Map Map Archipelago

**Short version: the code in this project was written almost entirely by an AI (Claude, by Anthropic). I directed
it, made the design decisions, reviewed every change, and tested everything in-game. Read on for exactly what that
means, so you can decide for yourself whether you want to play it.**

## What the AI did

- **Wrote essentially all of the code**: the apworld (items, locations, logic, options), the game mod (the BepInEx
  plugin), the automated tests, and the build/check scripts.
- **Did the reverse-engineering research**: reading the game's decompiled structure to find how islands, tools,
  missions and saves work, and building a small throwaway "probe" mod to confirm things at runtime.
- **Wrote the first drafts of the documentation** (README, setup guide, game page, design notes), which I then
  reviewed and edited.

## What I did

- **Every design decision.** Which items and checks exist, the starting island, which tools are required or just
  helpful, the goals and options, treasure rewards, and how sphere 1 works in multiworlds. These are
  recorded with reasons in the decisions log in [`DESIGN.md`](DESIGN.md).
- **The game knowledge the logic is built on.** Which islands need the Build Tool, where Glide is required, the
  Camera story step, what each mission type needs. This came from my own playthrough.
- **All in-game testing.** Every feature was played and checked in the actual game before being kept. Testing
  found and got fixed, among others: tools showing as locked, stickers and treasures mapping to the wrong checks,
  freezes after finishing an island and after a goal release, multi-mission progress resetting, and other islands'
  tutorials leaking onto new islands.
- **Reviewed every change.** The AI explained each piece of code in plain language before I accepted it, and I
  pushed back or changed direction when something didn't match how the game plays.

## What it is not

- **Not unreviewed or untested.** On top of in-game testing, the apworld has 41 automated tests (753 individual
  checks) run with Archipelago's own test framework, including tests that every logic rule gates exactly the right
  locations, plus 50+ test generations, solo and multiworld.
- **No AI art.** The current treasure picture is a self-made image trying to capture the crayon aspect of the cutscenes in the game.
- **No game code or assets** are included in this repository.

## Honest limitations

- I'm not an experienced programmer. I understand what each part of the mod does and why, and I can troubleshoot it
  with the logs and the explanations in the code, but I did not write it line by line.
- This is an early testing release (0.1.0). Known issues are listed in the [README](../README.md).

If you have questions about any of this, ask me directly. I'd rather you make an informed choice.
