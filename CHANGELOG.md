# Changelog

## 2.1.0 - Queue counts items you pick up

- A queued item now leaves the queue when you pick it up, not when the Command essence is opened. The essence
  drops the item on the ground, so before this the queue went down even if you never grabbed it.
- Opening another essence before picking up the last one skips the items still waiting on the ground, so the
  same queue slot isn't picked twice.
- Only permanent items count towards the queue. Temporary items (the ones that run out on a timer) and ethereal
  items don't use up a queue slot.
- Essences from printers and shops count the same as ones from chests.
- Items still lying on the ground when the stage ends no longer hold a queue slot.

## 2.0.0 - Risk of Rain 2 1.5, new menu, saves rework

**Game update**
- Updated for Risk of Rain 2 1.5.
- Added Hallowed and Hex queues. Boss, Lunar and Void queues are now on by default. Existing configs get every
  tier turned on once; after that your tier choices are kept.
- Tiers with nothing available in the current run (for example a DLC that isn't enabled) are hidden, unless you
  already queued items there.

**Fixes**
- Fixed item buttons in the queue menu showing as white boxes. Icons now load through the game's new icon system.
- Fixed item buttons in the menu having no tooltip. They now show the item's name and description.
- Fixed the queue running past its box when it holds many different items. It now wraps onto extra rows, and past
  3 rows the icons shrink to fit. Drag and drop works across rows.
- Fixed auto-picked Command essences giving the wrong item or nothing when LookingGlass is installed. The fix from
  LookingGlassCommandFix is built in, so that mod isn't needed with this one.
- Fixed queued item icons overlapping each other.
- Fixed the other tier buttons flashing their hover animation when one is selected.
- Fixed item icons flashing white the first time a tier is opened.
- Fixed the Saves page showing on top of the scoreboard.
- Fixed a crash in the menu when every tier was turned off in the config.
- Fixed the Enabled Tabs checkboxes in Risk of Options throwing an error and all showing as unchecked.

**New Command Queue menu**
- A sidebar on the left lists the item tiers with their color and how many items are queued.
- On the right, the selected tier's queue sits on top with a square repeat button at its end, and the items to add
  fill the space below and scroll instead of overflowing. Item buttons are 10% smaller than the game's command menu.
- Void tiers' color bars are split between the void color and the tier they mirror (Void Green is half void, half
  green), so the void tiers are easy to tell apart.
- Ctrl+click an item to queue 10 at once.
- The scoreboard, Command Queue and Saves pages are sized to the width and position of the item bar at the top of
  the screen, so they don't cover the HUD on the sides (MatchItemBarWidth, on by default).
- The game pauses while the Command Queue or Saves page is open, in single player only (PauseWhileQueueOpen, on by
  default). Works with both the held and the toggled scoreboard ("Toggle Inventory" in the game's control settings).

**Command Queue Preview**
- New HUD panel showing the next queued item of each tier during Command runs, and how many more are queued
  behind it.
- Sits under the objectives panel in the game's own style, so it fits any resolution or aspect ratio. Three entries
  per row, extending towards the screen edge; where three don't fit, the entries scale down instead.
- Falls back to a configurable position where the objectives panel doesn't exist.
- Can be turned off with "Show Command Queue Preview" in the settings.

**Saves rework** (inspired by CommandQueueExtended)
- Unlimited named saves with pages, rename, overwrite and delete. Overwrite and delete ask you to confirm.
- The name box and saves stretch across the page, with the overwrite / rename / delete buttons on the right.
- Saves are stored in `BepInEx/config/CommandQueue.saves.json`, so each mod profile has its own, instead of the
  game's folder.
- Old slot saves and CommandQueueExtended saves are imported once. The old files are left in place.
- Loading checks the whole save first: unknown items and items in turned-off tiers are skipped and reported,
  instead of wiping the queue and stopping at the first bad entry.
- Repeat settings are saved with the queue.
- Saves are written safely, so a crash while saving can't corrupt your saves file. A broken saves file is backed up
  instead of being overwritten.

**Other**
- Removed the unused R2API dependency.
- Development: game references now come from NuGet instead of a git submodule of prebuilt libraries.

## 1.7.0

- Updated to version 1.7.0 of the CommandQueue mod.
- Updated to work with Alloyed Collective.

## 1.6.1

- Fixed the Saves button so it works properly, instead of needing you to open Command Queue first.
- Side effect: the save and load buttons are smaller, but they are still a usable size.

## 1.6.0

- Deja vu of 1.5.0... I swear :D

## 1.5.1

- Moved the Saves button in line with Command Queue and Scoreboard, making it clearer.
- **Known issue:** the Saves button acts the same as the item rarity buttons, so you have to click Command Queue and
  then Saves to view the saves.
- Saved items now stay the same even if you add new items with mods or updates. The caveat is that items need to
  keep the same name, so if a mod developer renames sniper_scope to big_sniper_scope it will break, but that's
  better than before, where every time an item was added or removed your item list was effectively bricked.

## 1.5.0

- Uploaded the wrong DLL like an intelligent Neanderthal.

## 1.0.1

- Minor file updates to the readme for clarity. No gameplay impact.

## 1.0.0

- First update, fixing Command Queue with Save for the latest Seekers of the Storm version.
