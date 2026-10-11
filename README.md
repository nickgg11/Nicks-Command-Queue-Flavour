# Nicks Command Queue Flavour

> **AI disclosure:** I used AI to help write and update this mod's code and this page. I have tested the mod
> and play with it myself. If you find a bug, please report it on the
> [GitHub page](https://github.com/nickgg11/Nicks-Command-Queue-Flavour/issues).

Queue up the items you want, and every Command essence you open picks the next one for you. Perfect for
mega item runs: set up your build once, save it, and load it again next run.


Updated for **Risk of Rain 2 version 1.5**, including the new **Hallowed** and **Hex** items.

![The Command Queue page](https://raw.githubusercontent.com/nickgg11/Nicks-Command-Queue-Flavour/main/ReleaseAssets/blank_queue.png)

## Credits

This mod continues the work of others. Please go give them some love:

- [CommandQueue](https://thunderstore.io/c/riskofrain2/p/KubeRoot/CommandQueue/) by **KubeRoot**, the
  original mod: queues, auto-picking, drag and drop, repeat mode. ([Ko-fi](https://ko-fi.com/kuberoot))
- [CommandQueueWithSave](https://thunderstore.io/c/riskofrain2/p/Miniz/CommandQueueWithSave/) by **Miniz**,
  who added saving and loading queues. This mod started as a fix for it after it went inactive; I'll take it
  down if Miniz ever returns.
- [CommandQueueExtended](https://thunderstore.io/c/riskofrain2/p/gabrielgad/CommandQueueExtended/) by
  **gabrielgad**, whose named saves and all-tier queues inspired the reworked save system here.

## Why this one?

- **Works on 1.5.** Item icons show up (no white boxes), with tooltips, and Hallowed and Hex items can be queued.
- **LookingGlass compatible out of the box.** No more Command essences giving the wrong item or nothing at all.
  You don't need LookingGlassCommandFix with this mod.
- **Built for 10+ item tiers.** A tier sidebar instead of a row of tiny tabs, and a queue that wraps onto more
  rows instead of running off the edge.
- **Saves you can trust.** Unlimited named saves, stored per mod profile. Broken or outdated saves load what
  they can and tell you what was skipped, instead of wiping your queue.
- **Your old saves come with you.** Saves from Command Queue with Save and CommandQueueExtended are imported
  automatically.
- **See what's next without opening the menu**, with the Command Queue Preview on your HUD.
- **Take your time.** The game pauses while you build your queue in single player.
- **Fits your screen.** The menu lines up with the item bar at the top of the screen instead of covering the HUD.

## Features

### Queues
- A queue for each item tier: White, Green, Red, Boss, Lunar, Void White, Void Green, Void Red, Void Boss,
  **Hallowed** and **Hex**. Page items can be turned on in the config.
- When you open a Command essence, the next queued item of that tier is picked for you automatically.
- An item only leaves the queue once you **pick it up**. Open several essences before grabbing the drops and
  each one picks the next item in line.
- Only **permanent** items count. Temporary items (the ones that run out on a timer) and ethereal items don't
  use up a queue slot.
- **Click** an item to queue it, **Ctrl+click** to queue 10.
- **Drag and drop** to reorder the queue. Drag with right-click to move half a stack.
- **Click** a queued item to remove one, **right-click** to remove the whole stack.
- **Repeat mode** per tier: picked items go back to the end of the queue, for picking the same items forever.
- Items of the same kind next to each other merge into one stack.
- Tooltips with the item's name and description, on both the queue and the item list.
- Tiers you can't get this run (for example a DLC you have turned off) are hidden, unless you already queued
  items in them.
- Void tiers are marked with a split color bar (void color plus the tier they mirror), so Void White, Void
  Green, Void Red and Void Boss are easy to tell apart.

![An item tooltip, with Ctrl+click to queue 10](https://raw.githubusercontent.com/nickgg11/Nicks-Command-Queue-Flavour/main/ReleaseAssets/add_ten.png)

![A long queue wrapping onto more rows](https://raw.githubusercontent.com/nickgg11/Nicks-Command-Queue-Flavour/main/ReleaseAssets/queue_wrapped.png)

### Saves
- Unlimited named saves, with pages.
- Load, overwrite, rename and delete. Overwrite and delete ask you to confirm.
- Saves are stored in `BepInEx/config/CommandQueue.saves.json`, so each mod profile has its own.
- Repeat mode settings are saved with the queue.
- Saves keep working when you add or remove item mods: unknown items are skipped and reported.

![The Saves page](https://raw.githubusercontent.com/nickgg11/Nicks-Command-Queue-Flavour/main/ReleaseAssets/saves_page.png)

### Command Queue Preview
- Shows the next queued item of each tier on your HUD, and how many more are queued behind it.
- Sits under the objectives panel in the game's own style, and fits any resolution or aspect ratio.
- Can be turned off.

![The Command Queue Preview on the HUD](https://raw.githubusercontent.com/nickgg11/Nicks-Command-Queue-Flavour/main/ReleaseAssets/queue_preview.png)

### Quality of life
- Pauses the game while the Command Queue or Saves page is open (single player only).
- The scoreboard and queue menu are sized to the item bar at the top of the screen.
- Settings in the in-game options menu with [Risk of Options](https://thunderstore.io/c/riskofrain2/p/Rune580/Risk_Of_Options/) (optional).

## How to use

1. Start a run with the **Artifact of Command** enabled.
2. Hold **Tab** to open the scoreboard. Turn on **Toggle Inventory** in the game's controls settings to keep it
   open without holding the key.
3. Click **Command Queue** at the top.
4. Pick a tier on the left, then click items to add them to that tier's queue.
5. Open Command essences as usual: the queued items are picked for you. If the next queued item isn't
   offered, the normal menu opens.
6. Pick up the dropped item. It leaves the queue once it's in your inventory.

To save your queue, open **Saves**, type a name and click **Save current queue**. Click a save to load it.

## Configuration

Change settings in the game's options menu with Risk of Options, or edit
`BepInEx/config/com.kuberoot.commandqueue.cfg`.

![The mod's settings in the in-game mod options](https://raw.githubusercontent.com/nickgg11/Nicks-Command-Queue-Flavour/main/ReleaseAssets/mod_settings.png)

| Setting | Default | What it does |
| --- | --- | --- |
| EnabledQueues | All main tiers | Which item tiers get a queue. Toggle tiers under **Enabled Tabs** in Risk of Options. |
| PauseWhileQueueOpen | On | Pause while the Command Queue or Saves page is open. Single player only. |
| MatchItemBarWidth | On | Size the scoreboard and menu to the item bar at the top of the screen. |
| RightClickRemovesStack | On | Right-clicking a queued item removes the whole stack. |
| BigItemSelectionContainer | On | Use the whole page for the item list. Turn off for the game's command menu layout. |
| BigItemSelectionScale | 1 | Size of the item buttons. Lower it if you have a lot of modded items. |
| Show Command Queue Preview | On | Show the preview on the HUD (`[QueuePreview] Enabled` in the config file). |
| Fallback Position X / Y | 0.985 / 0.6 | Where the preview goes if it can't attach under the objectives panel (other game modes, HUD mods). |

## Compatibility

- **Don't install** together with KubeRoot's CommandQueue, Command Queue with Save or CommandQueueExtended.
  They do the same job and will conflict.
- Works with LookingGlass. LookingGlassCommandFix isn't needed.
- Multiplayer: only the players who want queues need the mod. Pausing only happens in single player.

## Changelog

See the **Changelog** tab on this page.

## Building from source

Requires the .NET SDK (6 or newer). Game and mod references come from NuGet, using the BepInEx package feed
in `nuget.config`, so no game files or extra repositories are needed:

```
dotnet build -c Release
```

The mod is `CommandQueue/bin/Release/netstandard2.1/CommandQueue.dll`. For a new game version, update the
`RiskOfRain2.GameLibs` and `MMHOOK.RoR2` versions in `CommandQueue/CommandQueue.csproj`.

To build and pack the Thunderstore zip in one step, run:

```
powershell -ExecutionPolicy Bypass -File package.ps1
```
