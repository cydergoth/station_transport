# Station Transport — a Star Valor mod

Put your ships on **trade routes** between your own stations and friendly NPC stations. Route
ships fly the route by themselves, through jump gates, and keep trading while you're elsewhere:
they haul goods between your bases and buy and sell at NPC markets for you.

> **Pre-release (0.1.0).** Seen working in a real game: ships flying a route through jump gates,
> saving and loading routes, the Hangar buttons and the galaxy-map panel. Not yet seen working:
> goods actually changing hands at each stop, the off-screen danger / emergency warp / repair
> cycle, splitting and looping routes, and the translations. Back up `Save_Data` before trying
> it, and please report what you find.

## Using it

### Making a route: the galaxy map

Open the galaxy map. A **Trade routes** panel sits on the left, under the map's own options;
its button opens and closes it.

1. Select a sector. Its stations that can trade are listed: your stations with a **Market** and
   a **Hangar**, and friendly NPC stations with a Market that you have discovered.
2. Press **New** next to a station to start a route there.
3. Select other sectors and press **Add** to add their stations as stops.

Ships run a route **there and back**: to the last stop, then back the same way. With three or
more stops you can **Close loop**, and ships go from the last stop back to the first. A loop
shows a closing "back to ..." row with **Open loop**.

To edit, pick a route with **<** / **>**. Click its name, or press **Rename**, to give it your own
name; clear the name to go back to the default "Route N". Each stop has **Up** (move earlier) and **Remove** (take
it out and join its neighbours). On a route that isn't a loop, stops between the ends also have
**Split**, which breaks the route in two there; ships go with the half they're heading for, and
an end left with a single stop is dropped. **Delete route** sends its ships back to the fleet.

Routes are drawn on the map along the jump gates their ships use, as dashed lines that roll
from each stop toward the next.

### Putting ships on a route: the Hangar tab

Dock at a station and open **Hangar**:

- **Fleet** list, a ship docked here: **Transfer to trade route**, then pick the route. The ship
  leaves your fleet: it no longer counts against your Fleet Commander limit and can't be launched.
- **Fleet** list, a route ship: **Remove from route**. Every route ship is listed under *Trade
  routes*, wherever it is, with its status. A ship in flight stops at its next stop. It rejoins
  your fleet if there's Fleet Commander room, otherwise it waits in reserve.
- **Cargo** list, a stored ship: **Route ship...** picks a route, then a captain, and puts the ship
  straight onto the route. This works even when your Fleet Commander limit is full.

In the fleet panel, route ships show a circling-arrows icon instead of Launch, a money-bag
**Trader** role icon, and their status, such as "To Alpha Station 1:23".

### What route ships do

At each stop a ship unloads, then loads goods that other stops want. What moves is set by each
station's **Market**:

- **Your stations** request goods set to *Buying* (up to the target stock) and provide goods set to
  *Selling* (above the target stock). Goods move between your stations without payment, and your
  own requests are filled before any sale.
- **NPC stations** buy and sell at their market price, paid from and to your credits. Ships don't
  spend more than `MaxSpendPerStop` at a stop, never take you below `KeepCredits`, and only buy to
  sell on at a profit of at least `MinMargin`.
- Cargo that no stop wants any more is dropped at the next station that takes it.

Ships only travel through jump gates: natural gates you have discovered, and jump gates you have
built. A stop with no gate route from the one before it is flagged **no gate route from ...** on
the map, and a ship that can't reach any other stop shows **Stuck: no gate route**. Build a jump
gate to link them and the ships move on by themselves. Speed comes from each ship's hull and
engines; the warp drive is only used for emergency warps.

### Danger and repairs

The game fights battles in sectors you aren't in. A route ship flying through a sector where a
faction hostile to you is fighting may be attacked (15% per such sector by default):

- Damaged ships carry on and are **repaired in full at their next stop**. Your own stations repair
  free; NPC stations charge their usual repair price. If you can't cover the bill, the ship is
  repaired anyway for whatever credits you have.
- A ship that would be destroyed **emergency-warps out** if it has a warp drive, arrives late,
  and is repaired at its stop.
- Without a warp drive it's **lost with its cargo**. The captain and crew survive and return to the
  station the ship last left.

### Limitations

- Route ships aren't drawn in space; they move as data. A later version may show them.
- Per-route goods rules aren't supported yet: every route uses each station's Market settings.
- Speed and cargo space are estimated from the hull and equipment, without crew bonuses, so
  trips take a little longer than a crewed ship would fly them.

## Installation

You need Star Valor with the **Base Building DLC**.

The mod needs **BepInEx 5**, the standard mod loader for Star Valor. If you already use other
Star Valor BepInEx mods, skip to step 2.

### 1. Install BepInEx 5

1. Download **`BepInEx_win_x64_5.4.23.2.zip`** from the
   [BepInEx 5.4.23.2 release](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.2).
   Use the **Windows x64** build of **BepInEx 5**, even on Linux. BepInEx 6 will not work.
2. Open the game folder. In Steam: right-click **Star Valor** → **Manage** → **Browse local files**.
3. Extract the zip into that folder. `winhttp.dll`, `doorstop_config.ini` and the `BepInEx` folder
   must end up **next to `Star Valor.exe`**, not in a subfolder.
4. **Linux / Steam Deck (Proton) only:** in Steam, right-click **Star Valor** → **Properties** →
   **General** → **Launch Options**, and enter:
   ```
   WINEDLLOVERRIDES="winhttp=n,b" %command%
   ```
   Without this, Proton ignores BepInEx and no mods load.
5. Start the game once, then quit. BepInEx creates `BepInEx/plugins`, `BepInEx/config` and
   `BepInEx/LogOutput.log`.

### 2. Install Station Transport

1. Download **`StationTransport-<version>.zip`** from the
   [Releases page](https://github.com/cydergoth/station_transport/releases/latest).
2. Extract it into the game folder (the same folder as `Star Valor.exe`). This creates
   `BepInEx/plugins/StationTransport/`, which holds `StationTransport.dll` and a `Language` folder.
3. Start the game and open the galaxy map. A **Trade routes** button appears on the left.

### Checking it worked

`BepInEx/LogOutput.log` should contain:

```
[Info   :Station Transport] Station Transport 0.1.0 loaded
```

and, after loading a save that has routes, `Loaded N trade routes and N route ships`. If the log
file doesn't exist, BepInEx isn't running. Check that the files are next to `Star Valor.exe`, and
on Linux check the launch option. If something else is wrong, please
[open an issue](https://github.com/cydergoth/station_transport/issues) and attach the log.

Press **Insert** for a log of recent route activity (arrivals, trades, repairs) and timings.

### Updating

Download the new release and extract it over the old one, replacing the files in
`BepInEx/plugins/StationTransport/`. If you edited a language file, back it up first.

### Uninstalling

- To remove only this mod, delete `BepInEx/plugins/StationTransport/`.
- To remove BepInEx too, delete `BepInEx/`, `winhttp.dll`, `doorstop_config.ini` and
  `changelog.txt` from the game folder, and clear the launch option.

Saves stay loadable without the mod. Route ships become ordinary fleet ships, docked at the last
stop they reached, with any cargo still aboard; unload it by hand. You may be over your Fleet
Commander limit until you make room. The mod's own files (`Save_Data/TradeRoutes_*.dat` and
`LastTradeRoutesBackup.dat`) can be deleted.

## Configuration

`BepInEx/config/starvalor.stationtransport.cfg` is created the first time the game runs with the
mod. Edit it while the game is closed.

| Setting | Default | Meaning |
|---|---|---|
| `DockSeconds` | `20` | Time a route ship stays docked at each stop |
| `GateTransitSeconds` | `15` | Extra travel time for each jump gate |
| `TravelTimeScale` | `1` | Multiplies flight time across sectors |
| `MaxSpendPerStop` | `20000` | Most credits a ship spends buying at one NPC station |
| `KeepCredits` | `10000` | Ships don't buy if it would leave you with less than this |
| `MinMargin` | `0.05` | Minimum profit (5%) for buying from an NPC station to sell on |
| `ExemptFromFleetCommander` | `true` | Route ships don't use Fleet Commander capacity |
| `OffscreenRisk` | `true` | Route ships can be attacked in sectors with hostile fighting |
| `RiskChance` | `0.15` | Chance of being hit in each such sector passed through |
| `EmergencyWarpDelaySeconds` | `60` | How late a ship that warped out of a fight arrives |
| `PanelOpen` | `true` | Whether the galaxy map's route panel is open. Its button changes this |
| `TogglePanel` | `Insert` | Opens the route activity log |
| `LogTrades` | `false` | Also write route activity to `BepInEx/LogOutput.log` |

## Translations

The mod reads the game's language setting and loads its own text from
`BepInEx/plugins/StationTransport/Language/<language>_main.txt`. These files use the same format
and names as the game's own `Language` folder (`german_main.txt`, `chinese_main.txt`, ...):

```
000 Transfer to trade route|
023 To <par1> <par2>|
```

- Each entry is a three-digit number, a space, the text, and a `|` at the end.
- Lines that start with `#` are comments.
- `<par1>`, `<par2>` ... are replaced with values, such as a ship or station name.
- Keep `<b>...</b>` tags around the same words.
- Save the file as UTF-8.

Station, sector and item names come from the game, so they are already translated. The route
activity log (**Insert**) is in English only.

The mod looks entries up by their number. If a file leaves an entry out, or the language has no
file, the mod uses the English text built into the DLL. Changing the language in game updates the
map panel straight away.

The non-English files were drafted with AI and may read awkwardly. To fix a translation or add a
language, copy `english_main.txt`, rename it to the game's name for that language, and translate
the text after each number. Please send corrections as a pull request or an issue.

## Compatibility

- Requires the Base Building DLC. The mod turns itself off in Arena mode.
- Built against Star Valor 2.2.5a (Steam build 25793649, Unity 2019.4.41f1) and BepInEx 5.4.23.2.
- Achievements are unaffected. The game has no mod detection, and this mod doesn't touch perks,
  stats or Steam.
- Nothing new goes into the game's own save files. Route data lives in
  `Save_Data/TradeRoutes_NN.dat` beside each save; Steam Cloud syncs it with the save.
- Has been played with the author's other mods installed (Extended Fleet Commands, Production
  Summary, Friendly Turret Coverage, Radar Scanner). The only game method it patches that they
  do too is `Lang.SetLanguageTo`, where Production Summary and this mod each just redraw their
  own UI after a language change.
- A game update that changes the fleet, hangar or galaxy map code could break parts of the mod. If
  that happens, the error is written to `BepInEx/LogOutput.log`, and the mod turns itself off at
  startup if it can't find what it needs.

## Building from source

You need the [.NET SDK](https://dotnet.microsoft.com/download) 6 or newer and a Star Valor
install, because the build references the game's own DLLs. Those DLLs are not included in this
repository.

```bash
./install.sh     # build and copy the mod into the game's BepInEx/plugins folder
./package.sh     # build and create the release zip in dist/
# or build only:
dotnet build -c Release -p:GameDir="/path/to/Star Valor"
```

`GAME_DIR` (for the scripts) and `-p:GameDir` (for `dotnet build`) default to the standard Linux
Steam location. `install.sh` also warns if it can't find a Steam account that owns the Base
Building DLC, by looking for its ownership ticket in Steam's
`userdata/<account>/config/localconfig.vdf`. That is undocumented, so the script only warns and
installs anyway. Set `STEAM_DIR` if Steam isn't in `~/.local/share/Steam`. The project file also
checks the default Windows Steam path.

### How it works

[FEATURES.md](FEATURES.md) is the design brief, and [FEASIBILITY.md](FEASIBILITY.md) maps it onto
the game's code. In short:

- **Route ships stay the game's own fleet ships** (`PlayerFleetMember`), tagged by their captain.
  Patches keep them out of the active fleet: `AIMercenaryCharacter.IsActive`,
  `GameManager.LaunchPlayerFleetMember`, `FleetMemberSlot.OrderLaunch` / `OrderDock`, and a
  postfix on `PChar.GetFleetSpaceOcupied` for the Fleet Commander exemption.
- **Travel is simulated as data**, timed by the game's play clock, so it pauses with the game. Ships
  path through `TSector.jumpGates` and linked `JumpgateStation`s. Stock changes are made while
  holding the game's station lock, taken without blocking.
- **Trading** reads each station's `SM_Market` items (`tradeMode`, `targetStock`) and changes stock
  through the station's `ItemStock`.
- **Saving**: `GameData.SaveGame` is wrapped so the route file is written only after the game's save
  succeeds, and ships in flight are saved as docked at their last stop, so removing the mod never
  strands one. Files are stamped with the save's play time and checked on load.
- **UI**: the Hangar buttons are clones of the inventory's own buttons; the galaxy-map panel and
  route lines are built in code on the map's canvas, using the game's gate-line material.

## AI disclosure

This mod's code and documentation were written with the AI coding assistant
[Claude Code](https://claude.com/claude-code), directed and reviewed by the author. Commits made
with it carry a `Co-Authored-By: Claude` trailer. If you share the mod on sites that label AI
content, such as Nexus Mods, tag it as AI-generated code.

Star Valor is made by Rafael Burgos. This is an unofficial fan mod, and the developer does not
endorse or support it.

## License

[MIT](LICENSE) © 2026 cydergoth. The license covers this mod's source code only. Star Valor and its
assets belong to their developer, and BepInEx has its own license.
