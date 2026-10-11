# CommandQueueWithSave DLC Fix

This mod is an extension of KubeRoot's commandqueue mod. Perfect for those who enjoy mega item runs. This mod allows you create a "list" of items that your command essences will become when picked up. Additionally it allows the user to save this list to be loaded at a later date. This mod is now Mod? compabitible. If you add or remove mods that add items your save/load orders will remain intact.

**ONWARD TO 200 BUNGUS!!**

This is a fixed version of the mod [Command Queue with Save](https://thunderstore.io/package/Miniz/CommandQueueWithSave/) that has been uploaded as the old maintainer has been inactive for a year.
I will take this mod down if Miniz ever returns.

I will add try to improve the mod to be more comfortable if I am able.

## Miniz Command Queue with Save

[original Mod](https://thunderstore.io/package/Miniz/CommandQueueWithSave/)

Adds save and load feature to this well known mod

## CommandQueue Mod

Original author :
[KubeRoot](https://ko-fi.com/kuberoot)

## Building

Requires the .NET SDK (6 or newer). Game and mod references are restored from NuGet, using the BepInEx
package feed listed in `nuget.config`, so no game files or extra repositories are needed:

```
dotnet build -c Release
```

The mod is `CommandQueue/bin/Release/netstandard2.1/CommandQueue.dll`. For a new game version, update the
`RiskOfRain2.GameLibs` and `MMHOOK.RoR2` versions in `CommandQueue/CommandQueue.csproj`.

# Release Notes

2.0.0

- Updated for Risk of Rain 2 1.5. Adds Hallowed and Hex queues.
- Fixed white boxes instead of item icons in the queue menu, and added item tooltips.
- The queue now wraps onto multiple rows.
- New Command Queue layout: tier sidebar on the left, the queue and a scrolling item grid on the right.
- Pauses the game while the queue or saves page is open (single player), and shows the next queued items on the HUD. Both can be turned off in the config.
- Fixed auto-picked command essences giving the wrong item when LookingGlass is installed (no need for LookingGlassCommandFix).
- Unlimited named saves with rename, overwrite and delete. Saves now live in `BepInEx/config/CommandQueue.saves.json`. Old slot saves and CommandQueueExtended saves are imported automatically.
- See CHANGELOG.md for the full list.

1.7.0.
- Updated to version 1.7.0 of CommandQueue Mod
- Updated to work for Alloyed Collective

1.6.1

- Fixed save load button so now it acts properly instead of needing the user to be in commandqueue for it function properly.
- Side effect is that the save and load buttons are smaller but they are still a usable size.

1.6.0

- Deja vu of 1.5.0... I swear :D

1.5.1

- Moved Saves button to be in line with command queue and scoreboard making it more clear.
- **KNOWN ISSUE** the saves button acts the same as the item rarity buttons, this means you have to click the commandqueue button then the saves button to view the saves.
- Item saves should now stay the same even if you add new items via mods or updates. Caveat being that the items need to stay the same name, so if a mod developer renames something sniper_scope to big_sniper_scope it will break but it's a better alternative to the previous option which is everytime an item is added or removed you item list is effectively bricked.

As a note: I will try to resolve the known issue stated above to the best of my abilities. I am unsure if/when that fix will come.

1.5.0

- Uploaded wrong DLL like an intelligent Neanderthal

1.0.1

- Minor file updates for readme and clarity. No Gameplay impact.

1.0.0

- Initial Update to fix for latest SOTS version.
