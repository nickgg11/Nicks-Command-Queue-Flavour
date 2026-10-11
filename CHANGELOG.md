### 2.0.0 - Game update (RoR2 1.5), saves rework

    - Update for RoR2 1.5.
    - Fix item buttons in the queue menu showing as white boxes. Icons now load through the game's async icon system.
    - Item buttons in the menu show a tooltip with the item's name and description.
    - The queue wraps onto extra rows instead of running past its box. Past 3 rows the icons shrink to fit.
      Drag and drop works across rows.
    - Fix the LookingGlass conflict where an auto-picked command essence gave the wrong item or nothing.
      Integrates the fix from LookingGlassCommandFix, so that mod isn't needed with this one.
    - Add Hallowed and Hex queues, and enable Boss, Lunar and Void queues by default. Existing configs get
      Hallowed and Hex added once; other tier choices are kept.
    - New layout for the Command Queue page: a sidebar on the left lists the item tiers with their color and how
      many items are queued. On the right, the selected tier's queue sits on top, with a square repeat button at
      its end, and the items to add fill the space below and scroll instead of overflowing. Item buttons are 10%
      smaller than the game's command menu. Tiers with nothing available in the current run (for example a DLC
      that isn't enabled) are hidden unless you queued items there.
    - Tooltips with the item's name and description on the item buttons.
    - Ctrl+click an item to queue 10 at once.
    - Configs still on the old default tiers (White, Green, Red) now get every tier enabled once.
    - Saves page: the name box and saves stretch across the page, with the overwrite / rename / delete buttons
      on the right.
    - The scoreboard, Command Queue and Saves pages are narrowed to the width and position of the item bar at the
      top of the screen, so they don't overlap the HUD on the sides (MatchItemBarWidth, on by default).
    - The game pauses while the Command Queue or Saves page is open, in single player only (PauseWhileQueueOpen).
      Works with both the held and toggled scoreboard ("Toggle Inventory" in the game's control settings).
    - HUD queue preview: shows the next queued item of each tier during Command runs, with how many more are
      queued behind it. It sits under the objectives panel as a copy of it (the same way LookingGlass places its
      stats panel), so it fits any resolution or aspect ratio. Three entries per row (tier bar, icon, how many
      more are queued), extending from the objectives towards the screen edge. Where three don't fit before the
      edge, the entries scale down instead.
    - Fix queued item icons overlapping when the queue has many different items.
    - Void tiers' color bars are split between the void color and the tier they mirror, e.g. Void Green is half
      void, half green.
    - Fix the other tier buttons flashing the hover animation when one is selected.
      Falls back to a configurable position where that panel doesn't exist. Can be turned off (QueuePreview section).
    - Fix the Enabled Tabs checkboxes in Risk of Options throwing InvalidCastException and all showing as unchecked.
    - Saves rework, inspired by CommandQueueExtended:
      - Unlimited named saves with pagination, rename, overwrite and delete (overwrite and delete ask for confirmation).
      - Saves are stored in BepInEx/config/CommandQueue.saves.json, per profile, instead of the game's working directory.
      - Old slot saves and CommandQueueExtended saves are imported once. The old files are left in place.
      - Loading checks the whole save first: unknown items and items in disabled tiers are skipped and reported,
        instead of wiping the queue and stopping at the first bad entry.
      - Repeat toggles are saved with the queue.
    - Fix the Saves page showing on top of the scoreboard.
    - Fix a crash in the menu when every tier was disabled in the config.
    - Remove the unused R2API dependency.
    - Development: game references now come from NuGet (RiskOfRain2.GameLibs, MMHOOK.RoR2) instead of a git
      submodule of prebuilt libraries.

### 1.7.0 - Game update

    - Update for V1.4.1 - Alloyed Collective update
    - Reduce dependency on vanilla item tiers - this might allow modded tiers to show up in queues, untested.
      As a side effect, tier names have changed to include "Def" at the end, to match internal ItemTierDefs.
      Backwards compatibility should be maintained, configuration will automatically update to match new values.
    - Add experimental optional Risk of Options integration. Should work fine without RoO installed, please report
      any issues.

### 1.6.1 - Bugfix

    - Fix issue where items would not be automatically selected from command droplets.
      This was caused by trying to get a reference to the command picker panel too early, which wasn't an issue when hot-reloading in development.

### 1.6.0 - Game update

    - Update for V1.3.1 - Seekers of the Storm update

### 1.5.0 - Game update

    - Update for Devotion Update

### 1.4.0 - Game update

    - Update for SotV patch 1.2.3

### 1.3.3 - Hotfix

    - Fix item icons having the wrong scale when advancing to the next level, which caused items in the queue to start overlapping.

### 1.3.2 - Looping mode

    - Add looping mode. If the button is enabled, when commandqueue chooses an item, the chosen item is added back at the end of the queue.

### 1.3.1 - Plug my Ko-fi

### 1.3.0 - Game update, extra features

    - Update the mod for the Survivors of the Void update
    - Add drag'n'drop for items in the queue
    - Add an option to remove a whole stack of items in the queue with right-click, enabled by default
    - Merge adjacent stacks of the same item in the queue, happens when an item between them is deleted
    - Stop queue items getting focus when clicked. Will prevent issues where game doesn't respond to input until you click.

### 1.1.0 - Extra features

    - Add expanded menu option (enabled by default)
    - Add support for Lunar and Boss item queues (disabled by default)
    - Change source of item rarities, should help compatibility in some cases
    - Minor improvements to UI (figured out pivots, things can be more dynamic)

### 1.0.0 - Initial release


### 1.0.1 - Remove a debugging message from being printed in chat

### 1.0.0 - Initial release