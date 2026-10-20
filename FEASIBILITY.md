# Station Transport: Feasibility Study

Assessment of implementing the trade-route mod described in [FEATURES.md](FEATURES.md)
against the locally installed Star Valor.

## Verdict

**Feasible, but this is the largest mod in the set by a wide margin.** Every
requirement can be met with BepInEx 5 + Harmony patches, using the same toolchain as
the four sibling mods. The game supplies most of the *data* pieces this mod needs:
- stations exist and can be updated while their sector is not loaded
- markets already have a provide/request scheme
- fleet ships already need a captain
- natural jump gates form a usable sector graph

What the game does **not** provide, and the mod must build from scratch:

1. **A travel model.** No ship in the game actually travels between sectors. NPC traders
   and the player's own fleet are data records that teleport instantly. Route
   planning, ETAs and an "in transit" state are all new code.
2. **Persistence.** The save is one `BinaryFormatter` graph. Mod data has to live in a
   sidecar file, or the mod could never be uninstalled.
3. **A station picker on the galaxy map.** The map only lists stations as text. Stations
   cannot be selected there today.

Rough size: 4–6k lines of C#. The four existing mods together total about 5.5k.

## Environment checked

| Item | Status |
|---|---|
| Game install | `~/.local/share/Steam/steamapps/common/Star Valor`. Windows build running under Proton; Steam app 833360 |
| Game version | `GameData.currVersion = 2.25`, sub-version `a` (displayed as 2.2.5a) |
| Unity | 2019.4.41 (Mono, so Harmony patching works) |
| BepInEx | 5.4.23.2, installed and loading the 4 sibling plugins (see `BepInEx/LogOutput.log`) |
| Build tooling | .NET SDK 8 in `~/.dotnet`. The csproj template is identical across siblings (net472, `BepInEx.Core` 5.4.21, references into `Star Valor_Data/Managed`) |
| Decompiler | `~/.dotnet/tools/ilspycmd` works with `DOTNET_ROOT=~/.dotnet DOTNET_ROLL_FORWARD=Major` |
| Base Building DLC | Detected by the game with `GameData.HasExpansion(Expansion.BaseBuilding)`. The mod must check this and disable itself if the DLC is missing |

Note: `dotnet` is not on `PATH` in a plain shell. The sibling `install.sh` scripts
assume it is.

## Requirement-by-requirement assessment

Legend: 🟢 straightforward · 🟡 moderate work · 🔴 significant work / design decision

| # | Requirement | Rating | Notes |
|---|---|---|---|
| 1 | Pick an idle ship at a player station and deploy it on a route | 🟢 | Fleet ships parked at a station are `PlayerFleetMember`s with `dockedStationID == station.id` (`PChar.GetStationDockedAIFleetMembers()`) |
| 2 | Requires equipment, crew and a captain | 🟢 | A stored hull is turned into a fleet member through `Inventory.AddToFleet(captain)` / `AssignCaptainDialog`, which already requires a captain. Route ships are **exempt from Fleet Commander capacity**, so the mod adds its own commission path without the capacity check (see [Fleet Commander exemption](#fleet-commander-exemption)) |
| 3 | Routes between player stations and friendly NPC stations | 🟢 | `Station.IsPlayerFriendly` (reputation rank ≥ 0) works off-sector. Re-check it on each arrival, since reputation changes |
| 4 | Player stations need Market + Landing Dock + Trading module | 🟡 | Market = `SM_Market` (id 50), "landing dock" = `SM_Hangar` (id 40, `Station.HasDocking()`). Trading module: see [the module decision](#decision-1-trading-module) |
| 5 | Multi-stop routes, ping-pong or circular, many ships per route, a station on many routes | 🟢 | Pure mod data |
| 6 | Sectors must be linked by warp gates | 🟡 | `TSector.jumpGates[].connectedSector` gives the natural gate graph. Player-built `JumpgateStation.connectedJumpgateStation` adds extra edges. No pathfinding exists, so the mod needs a BFS. Route only through `discovered` gates |
| 7 | Warp engine irrelevant except for emergency warp | 🟢 | We control travel, so warp range is simply ignored |
| 8 | Emergency-warped ships return and resume once repaired | 🟡 | `AIMercenary.EmergencyWarp()` only runs while the ship is spawned, and sends it to the "nearest sector" as an *away* fleet member. Patch it to tag route ships, then have the route logic send them to the nearest route station to repair. **Gap:** I found no automatic repair for docked fleet members. The mod may need to charge for repairs itself |
| 9 | Per-route provide/request at each station; deliver to the highest payer first, up to the request limit | 🟡 | Prices: `Station.GetMarketPriceForItem(item, mode, out stock)`. Stock: `ItemStock.AlterStock` / `SM_Market` APIs. Request limits map onto `DynamicMarketItem.targetStock`. See [design issue: pricing at player stations](#design-issue-pricing-at-player-stations) |
| 10 | Ships keep trading when the player is elsewhere | 🔴 | Needs a new abstract simulation. See [the travel model](#decision-3-travel-model) |
| 11 | Cargo no longer requested is dumped at the next station | 🟢 | Pure route logic |
| 12 | Travel at rated engine speed; configurable dock time | 🟡 | Speed comes from `ShipModelData` plus equipment, but the live stats are only computed on a spawned `SpaceShip`. The mod must reimplement the speed and cargo-capacity calculation (around `SpaceShip.cs:3276`) for unspawned ships, or cache it the last time the ship was spawned |
| 13 | Galaxy map: pick a discovered station, start/extend/edit a route | 🔴 | `GalaxyMap.OnSectorSelect` is a public event; hook it to show a new mod panel listing the sector's stations as buttons. No sibling mod touches `GalaxyMap` yet, so this is new UI ground. Route lines can copy how `InstantiateJumpGates` draws lines |
| 14 | Removing a middle station splits the route (unless circular) | 🟢 | Pure data logic. As built: **Remove** joins the neighbours and **Split** breaks the route in two, dropping an end left with one stop (its ships stay with the other half). A loop is closed explicitly back to its first stop (3+ stops); splitting doesn't apply to loops |
| 15 | Station UI: assign an idle ship; list and unassign route ships | 🟡 | Add a button to the fleet list (`Inventory`, `cargoMode == 2`) by cloning `BtnAddToFleet`. Add a "Trade Routes" docking tab using the pattern proven in Production Summary's `DockingUIPatches` |
| 16 | Ship off-route-station flies to the nearest route station first | 🟢 | Falls out of the BFS |
| 17 | Unassigned ship stops at the next station | 🟢 | It ends as a normal docked fleet member there |

## Key design decisions

### Decision 1: Trading module

Answers the open question "separate trading module or the existing market".

It is **technically possible** to add a new module type: Harmony postfix on the private
`SM_DB.Validate()` and append a `StationModule` subclass with a free id such as 120.
The build list (`StationManagement.LoadModulesList`) picks it up automatically.

But there are two serious costs:

- **Save lock-in.** Modules are saved as objects inside `station.modules` by
  `BinaryFormatter`, which records the mod's assembly name. If the mod is removed, the
  save **will not load**. A `SerializationBinder` hack could work around this, but it
  is fragile.
- **Bespoke UI.** The module config screen (`StationModuleConfig.cs:210-275`) is a prefab
  driven by hardcoded `is SM_Market` / `is SM_Fabricator` branches. A new module gets
  no config UI unless it is patched in.

**Recommendation:** no new `StationModule` in v1. A player station takes part in trade
routes when it has a Market and a Hangar and the player adds it to a route. (An earlier
plan also required a per-station "trade routes enabled" switch in place of the module. It
was dropped in phase 3: adding the station to a route is already the opt-in, and the switch
only made newly added stops unusable until found.) Provide/request uses the market's existing per-item settings:
- `tradeMode`: -1 = buying (request), 1 = selling (provide), 0 = both
- `targetStock`: the request limit, or the minimum stock to keep when providing

Per-route overrides sit on top of these. The scheme FEATURES.md asks for therefore
already exists, with a working UI (`SM_MarketItemSlot`).

If a separate, purchasable module is still wanted for game balance, it can be added in a
later phase. That means accepting "the mod cannot be removed from a save that used it",
which is how the bigger Star Valor content mods behave.

### Decision 2: How a route ship is represented

**Recommendation:** route ships stay `PlayerFleetMember`s, and the mod tags them in its
own data.

- **What this gets for free:** the captain/crew requirement, the loadout and cargo
  (`aiChar.shipData`), the spawning code (`GameManager.SpawnMercenary`), and it stays
  valid if the mod is removed.
- **What it costs:** the vanilla fleet logic must be patched to ignore tagged ships.
  Otherwise they spawn next to the player, follow warps, dock with the player and can be
  recalled. Expected patch points:
  - `AIMercenaryCharacter.IsActive`
  - `GameManager.SpawnPlayerFleet`
  - `PlayerControl.WarpToSector`
  - `GameManager.LaunchPlayerFleetMember`
  - the fleet panel lists
- **Fleet Commander capacity: route ships are exempt** (design decision). See
  [Fleet Commander exemption](#fleet-commander-exemption).
- **Uninstall safety:** while a ship is "in transit" in mod data, the vanilla save should
  still record it as docked at the last station it left (`dockedStationID`,
  `sectorIndex`). If the mod is removed, every route ship then reappears docked
  somewhere sensible.

#### Fleet panel status: reusing vanilla "Away"

Proposal: show route ships with the vanilla "Away" state and give the launch/dock
button a third "on trade route" icon, so they cannot be called to the player.

How vanilla decides the state. It is derived, not stored
(`PlayerCharacter.GetAwayAIFleetMembers`, `AIMercenaryCharacter.IsActive`):

| State | Condition |
|---|---|
| Away | `dockedStationID == -1`, not `hangarDocked`, `sectorIndex != currentSectorIndex` |
| Docked | `dockedStationID >= 0`, in any sector |
| Active (spawned, follows player) | `dockedStationID == -1` and in the current sector |

What it gives:

- **Ship speed: nothing.** Away and docked ships are not spawned, and vanilla computes
  no live stats for them. `FleetMemberSlot.UpdateBars` hides their bars. The speed
  problem in requirement 12 stays separate: cache stats from the last spawn, or
  reimplement the calculation.
- **HP: yes.** `shipData.HPPercent` works without a spawned ship. Hangar ships already
  show "Repairing NN%" from it, so route ships can show HP and repair state cheaply.
- **Status text: yes.** Unspawned, non-hangar ships get an empty `statusText`. A
  postfix on `UpdateBars` can write "→ Station X, ETA 3:20" or "Loading at Y", so the
  fleet panel doubles as a route overview.
- **Listing: yes.** Route ships appear in the fleet panel with no new list code, and
  their state matches emergency-warped ships, which vanilla already marks Away.

Catches:

1. **Away does not cover the player's own sector.** A ship in transit with
   `dockedStationID == -1` in the player's sector makes `IsActive()` return true, so it
   would spawn and join the fleet. The `IsActive` / `SpawnPlayerFleet` patches are
   still required.
2. **The third icon only shows the lock; it doesn't block anything.**
   - Launching an Away ship calls `WarpInFromAnotherSector`, which warps it to the
     player if it has a warp drive.
   - "Launch fleet" calls `OrderLaunch` on every slot (`FleetControl.LaunchFleet`),
     skipping the button.
   - So the guard goes in `FleetMemberSlot.OrderLaunch` / `OrderDock`, plus the
     per-ship dock hotkey (`FleetControl`, `behavior.dockKey`).
   - The icon itself is easy: `btnLaunchDock` already switches between `ImgDock` and
     `ImgLaunch` children, so clone one as a third child. Patch the mode-3 tooltip in
     `FleetMemberSlot.ButtonEnter` to match.
3. **Uninstall safety gets worse.** A ship saved as Away (`-1`) is stranded after an
   uninstall if it has no warp drive, and route ships don't need one (vanilla shows the
   "can't warp" warning). Fix: write the last station into `dockedStationID` for the
   save, then restore `-1`.
   - A postfix on `GetGameData` is **too early** to restore. `GameData.SaveGame` calls
     `GetGameData(isSaving: true)` on the main thread, then serialises `data` later
     inside `Task.Run`.
   - Instead: a prefix on `GetGameData(isSaving: true)` writes the station ids (it may
     run up to 5 times on retries, so keep it idempotent). A postfix on
     `GameData.SaveGame` replaces `__result` with a wrapper: `await` the original task,
     then restore `-1` in a `finally`. Unity resumes `await` on the main thread, so the
     restore is thread-safe.
   - The route tick must pause while a save is running. The main thread keeps running
     frames during `Task.Run`, and changing route state mid-serialise would race.
4. **Why not leave `dockedStationID` at the last station all the time?** It would avoid
   the save fixup, but several `Inventory` checks compare `dockedStationID ==
   currStation.id` directly (the fleet list, `CallEditFleetShip`, the add/remove fleet
   buttons). An in-transit ship would then appear docked at the station it left. Setting
   `-1` while in transit keeps those checks correct for free.
5. **Refit while docked.** A route ship docked at the player's current station passes
   the `Inventory` checks and can be refitted or have cargo moved by hand. Either block
   this while the ship is assigned, or allow it and re-read cargo and stats when the
   ship next departs.

**Recommendation:**

- **Docked at a route station:** keep the real `dockedStationID` and show the route icon.
- **In transit:** set `dockedStationID = -1` so the panel shows Away. Keep the
  `IsActive` patch for the player's sector, and do the save-time fixup with the
  `SaveGame` task wrapper.
- **Speed:** handle separately (see requirement 12).

#### Fleet Commander exemption

Ships assigned to a trade route do not count against the Fleet Commander limit. 🟢
Easy, with one loophole to close.

How vanilla handles capacity:

- `PChar.GetFleetSpaceOcupied()` (PChar.cs:275) sums `fleetSpaceOcupied` over every
  `PlayerFleetMember`, docked or not. `GetFreeFleetSpace()` is the Fleet Commander
  value minus that sum.
- The **only** place capacity is enforced is `Inventory.AddToFleet` (Inventory.cs:2406),
  when a stored hull becomes a fleet ship. The fleet list header
  (`occupied/total`, Inventory.cs:1864) and the free-space display (Inventory.cs:2156)
  read the same functions. The carrier hangar has its own separate limit
  (`CarrierControl`), which this change does not affect.

Implementation:

1. **Postfix `PChar.GetFleetSpaceOcupied`** to subtract the `fleetSpaceOcupied` of every
   ship tagged as a route ship. That one patch fixes the capacity check, the header and
   the free-space display.
2. **Assigning a stored hull straight to a route.** At full capacity, vanilla
   `AddToFleet` would refuse. The mod needs its own "Commission for trade route" path:
   - pick a captain with the existing `AssignCaptainDialog`
   - repeat `AddToFleet`'s short body without the capacity check (`GetShipLoadout`, the
     `RemoveItem` / `DeleteShipLoadout` / `ConsumeItem` calls, then
     `CreatePlayerFleetMember`)
   - tag the new ship as a route ship before anything reads capacity again
3. **Closing the loophole: a route ship must never become a normal fleet ship that
   bypasses the limit.**
   - Route ships cannot be launched, undocked or recalled into the active fleet. The
     fleet-exclusion patches above already cover this.
   - **Unassigning** checks free capacity:
     - If there is room, the ship becomes a normal docked fleet ship.
     - If there isn't, the ship stays docked and the player gets two choices: keep it in
       reserve as an unassigned route ship (still exempt, but unable to launch), or
       *Remove from fleet*. That calls the vanilla `Inventory.RemoveFromFleet`
       (Inventory.cs:1590), which turns the ship back into a stored hull and returns the
       captain as crew.
   - **If the mod is uninstalled,** route ships become ordinary fleet ships again and the
     player may be over the limit. Vanilla tolerates this: nothing outside `AddToFleet`
     enforces capacity, so the only effect is that no new fleet ships can be added until
     there is room. Document this in the README.

Optional balance lever: a BepInEx config setting to turn the exemption off, or to count
route ships at a fraction of their normal fleet space.

### Decision 3: Travel model

**Recommendation:** an abstract, data-only simulation, ticked from the plugin's `Update`
on the main thread and timed by `GameData.timePlayed`.

- `timePlayed` stops while the game is paused and is saved with the game.
  `CharacterSystem` uses the same clock.
- Each route ship has a state: `Docked(stationId, untilTime)` or
  `Transit(leg, departTime, arriveTime)`.
- A leg's duration is the in-sector distance from station to gate, plus a per-gate
  transit cost, plus the distance from gate to station, all divided by speed. Station
  and gate coordinates are both available (`Station.x/y`, `JumpGate.x/y`).
- Market and stock changes are made while holding `GameData.threadSaveLock`, because the
  background "Stations Thread" updates station modules at the same time. Take it with
  `Monitor.TryEnter(lock, 0)` and retry next frame, never with a blocking `lock`. The
  Stations Thread can hold it for hundreds of milliseconds (see
  [CPU impact](#cpu-impact-swag)).
- **Visual presence (phase 4):** when the player is in a sector where a route ship is in
  transit or docked, spawn it with `SpawnMercenary` (private; call via reverse patch)
  using a small custom controller: fly to the target station or gate, then despawn. The
  `AITraderControl` flow (`StationDockingReached`, `WarpDisappear`) is the template. If
  the spawned ship is destroyed, the vanilla `Die()` applies: the ship and cargo are
  lost and the captain drops as loot.
- **Off-screen danger:** none in v1, because the game does not simulate off-screen combat
  for anything. Optionally add a risk roll later via `TSector.IsBeingAttacked`.

### Decision 4: Persistence

Answers "how will these features be recorded in save files".

The data goes in a **sidecar file** per save slot, e.g.
`Save_Data/TradeRoutes_{slot:00}{_Auto?}.dat`. The content can still be JSON, but the
extension must be `.dat` (see [Steam Cloud](#steam-cloud-and-save-consistency)). It
holds the routes, the per-route goods settings, the ship assignments and their travel
state, and the station-enabled flags.

| Event | Hook |
|---|---|
| Write | Snapshot in a postfix on `GameData.GetGameData(isSaving: true)` (main thread). Write the file from the `SaveGame` task wrapper once the vanilla save has succeeded, via temp file + `File.Replace` like vanilla |
| Read | Postfix the private `GameData.SetGameData` when it succeeds |
| Clear | `GameData.NewGame` |
| Delete | `MenuControl.DeleteSaveGame` / `DeleteAutoSaveGame` |

- **Keys:** stations by `Station.id`, which is unique and stable. Ships need a stable key
  for `PlayerFleetMember`. Check whether the mercenary/crew id is unique, or tag the ship
  through an unused field the game already saves, the way Extended Fleet Commands uses
  `FleetMemberBehavior.repairTactic`. If you use that field, pick a different bit:
  Extended Fleet Commands already sets `OnlyMiningGearFlag` in it (`MiningGear.cs`).
- **Missing or stale sidecar** (e.g. a save copied without it): route ships simply stay
  docked. Never crash.

#### Steam Cloud and save consistency

Star Valor uses Steam Auto-Cloud (app 833360). From the local `appinfo.vdf`, the synced
patterns are:

| Root | Folder | Pattern |
|---|---|---|
| gameinstall | `Save_Data` | `*.dat` |
| gameinstall | `Star Valor_Data` | `general.dat`, `general_Bkp.dat`, `defaultKeys.dat` |

What follows:

- **The sidecar must end in `.dat` and live in `Save_Data`.** A `.json` file there, or
  anything under `BepInEx/`, is never uploaded. On a second machine the save would load
  with no routes. Naming it `TradeRoutes_NN.dat` gets it synced with no setup, and
  doesn't look like a vanilla `SaveGameData_*` file.
- **Steam syncs each file separately.** A cloud conflict, an interrupted sync or a
  manual copy can pair a save with a sidecar from a different point in time. A failed
  vanilla save (it retries 5 times, then gives up) can do the same if the sidecar has
  already been written.
  - Stamp the sidecar with values the vanilla save also records: `totalTimePlayed`, plus
    `SaveFileInfo.lastSaveDateTime` from the `GetGameData` result.
  - On load, compare the stamp. If it doesn't match, treat the sidecar as stale: keep
    the routes and settings, drop the travel state, and leave every route ship docked
    where the vanilla save puts it. Log a warning.
  - Writing the sidecar only after the vanilla save succeeds removes the failed-save case.
- **Backups.** Vanilla keeps two:
  - `SaveGameData_NN_Bkp.dat`: a copy of the main save, made on load at most every
    5 minutes (`VerifySaveFileBackup`).
  - `LastSaveGameDataBackup.dat`: the previous version of whichever slot saved last
    (`File.Replace`).
  
  The game never loads either one; they are for players restoring by hand. Mirror them
  (`TradeRoutes_NN_Bkp.dat`, `LastTradeRoutesBackup.dat`) so a restored backup has
  matching routes. Without a mirror, the stamp check handles it.
- **Delete.** `MenuControl.DeleteSaveGame` removes the main, auto, info and `_Bkp`
  files. Remove the sidecar and its `_Bkp` there too, or Steam keeps syncing orphans.
  `DeleteAutoSaveGame` removes only the auto save, so remove only the `_Auto` sidecar.
- **Size.** Saves are 5–10 MB; the sidecar will be a few KB, so sync cost is negligible.

### Design issue: pricing at player stations

- A player station's "credits" are the player's wallet (`Station.AddCredits` /
  `PayCredits` → `PlayerControl.inst.GetCargoSystem`). So "selling" goods from one player
  station to another is really a **stock transfer** with no money involved. "Highest
  paying station" only makes sense for NPC stations.
- **Proposed rule:**
  - deliveries to player stations are free transfers, ranked by need
    (`targetStock - stock`)
  - deliveries to NPC stations are sales at `GetMarketPriceForItem`, credited to the
    player
  - purchases from NPC stations are paid from the player's wallet, with a per-route
    spending cap
- **Watch out:**
  - `SM_CargoTransfer` shares stock between linked player stations
    (`stockLinkedStation`), so two linked stations must not be counted twice.
  - Player stock includes the player's stash (`GetStockCountWithStash`), which
    complicates counting the same way.

## Answers to the open questions in FEATURES.md

| Question | Answer |
|---|---|
| How will features be recorded in save files? | In a sidecar `.dat` file per save slot (so Steam Cloud syncs it); nothing new goes into the vanilla save (see [Decision 4](#decision-4-persistence)) |
| What UI buttons and panels are needed? | (1) Galaxy-map station picker and route editor panel. (2) Route lines on the galaxy map (optional). (3) "Assign to route" button in the docked fleet list. (4) "Trade Routes" docking tab: routes touching this station, per-route goods, and assigned ships with unassign. (5) Route overview with ship states and ETAs, reachable from the map |
| Any UI gaps? | Yes, three: there is no way to select an individual station on the galaxy map; there is no place to edit per-route-per-station goods (vanilla market slots are per station); and the docking tab bar is already crowded (see [Risks](#risks)) |
| Separate trading module or the existing market? | Reuse the Market in v1; adding a station to a route is the opt-in (see [Decision 1](#decision-1-trading-module)) |
| Should stations with factories auto-request their inputs? | The game already does this. `SM_Market.AddStationProductionAsMarketItems` adds factory outputs as "selling" and inputs as "buying" market items. Routes inherit it if they read market settings by default |

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Spawned route ships getting pulled in by vanilla fleet logic | Ships follow the player or teleport | Central `IsRouteShip()` check, patched into every fleet-list entry point. Test warp, dock, launch and load |
| Players using the capacity exemption to grow a combat fleet past the Fleet Commander limit | Exploit | Route ships can't launch or undock; unassigning requires free capacity (otherwise the ship stays in reserve or is removed from the fleet) |
| Background-thread races on station stock | Corrupted stock or exceptions | All mutations under `GameData.threadSaveLock` from the main thread, taken with a non-blocking `TryEnter` |
| Reimplemented speed/cargo stats drifting from the game's | Wrong ETAs or over-filled holds | Cache the real stats from the last spawn; fall back to the base model values |
| Docking tab bar crowding | Production Summary already uses panel code 5 and its `FitButtons` assumes ≤5 tabs | Use panel code 6, and rework `FitButtons` in both mods. Or put trade routes inside the existing management panel (`StationManagement`, player stations only) instead of a new tab |
| Game updates renaming private members | Mod breaks | The sibling `GameAccess` pattern: bind non-public members at startup and disable cleanly on failure |
| Uninstalling the mod mid-route | Cargo stuck on ships | Ships stay docked fleet members with their cargo in the hold, and the player can unload manually (`FleetControl.UnloadFleetCargo`). Document this in the README |
| Small player bases (`SmallBase`) are not in `stationList` | Can't be found off-sector | Not a real issue: Markets can't be built on Hideouts, so only full stations qualify |

## CPU impact (SWAG)

**These figures are an unmeasured estimate**, worked out from the decompiled code
and typical Mono costs. Phase 1 should replace them with `Stopwatch` measurements.

Assumed heavy late-game load: 10 routes, 40 ships, 6 stops per route, 20 goods per
route, a full round trip every ~90 s of game time per ship.

| Work | When | Estimated cost |
|---|---|---|
| Travel tick: compare each ship's next-event time with `timePlayed` | Every frame | 40 float compares, **< 0.01 ms/frame**. A min-heap keyed on next-event time makes it O(1), but isn't needed at this scale |
| Trade decision on arrival: each good × each route station × `GetMarketPriceForItem`, which does a linear `List.Find` over that station's market (~50–100 items) | ~0.5 arrivals/s across the fleet | ~12k predicate calls, **~0.1–0.5 ms** in a single frame per arrival. Averaged out this is noise |
| Stock and credit changes | Per arrival | A few dozen `AlterStock` calls, **< 0.1 ms** |
| Gate-graph BFS | Only when a route is edited or a gate is built or discovered; cached | Hundreds of sectors, **< 1 ms**, one-off |
| Fleet panel status text | Every 0.2 s per visible slot (vanilla `UpdateBars` cadence) | String format, a few µs and ~100 B of garbage per slot. Negligible |
| Galaxy-map route lines | When the map opens or a route changes | Comparable to vanilla's jump-gate lines. Negligible |
| Sidecar save | Per save | A few KB of JSON, **< 5 ms**, once per save from the `SaveGame` wrapper (main thread is fine at this size). Vanilla is already serialising 5–10 MB of `BinaryFormatter` data at that point |
| **Visual presence (phase 4)** | Every frame, only for route ships in the player's sector | Each spawned route ship costs the same as one vanilla NPC trader (physics, AI, rendering). This is the **only cost that scales visibly**. Cap the number spawned per sector with a config setting (e.g. 5) and keep the rest abstract |

**Steady state: well under 0.1 ms/frame, < 1% of a 60 fps frame budget**, with
occasional sub-millisecond spikes on arrival. For comparison, vanilla's Stations
Thread already simulates every station plus 100–350 off-screen NPC traders
(`CharacterSystem`: `50 + galaxy.size × 10`, +50 for non-fixed-size galaxies). The
mod's abstract simulation is at least an order of magnitude smaller than that.

**The real risk is lock waits, not computation.** The Stations Thread holds
`GameData.threadSaveLock` across whole passes:
- every station's `UpdateModules`
- every sector's `SlowUpdate` and price update
- `CharacterSystem.UpdateCharacters`, which calls `Thread.Sleep(1)` per NPC character
  *inside* the lock. With 100–350 characters, Windows timer granularity and Proton,
  that is likely **0.1–0.6 s**, every 30 s of game time.

A main-thread `lock (threadSaveLock)` that lands during that pass would freeze the game
for that long. With `Monitor.TryEnter(lock, 0)`, the arrival is processed a few frames
late instead, which the player can't see. This needs to be a hard rule in the code
(see [Decision 3](#decision-3-travel-model)).

## Further considerations

| Topic | Notes |
|---|---|
| Arena mode | `GameData.data.gameMode == 1` is Arena. Vanilla emergency warp already returns early there. The mod should disable itself entirely in Arena: no tick, no UI, no sidecar |
| Route station becomes invalid | An NPC station can be destroyed (`Station.DestroyedOrWrecked`) or captured, or the player's reputation can drop below friendly. A player station can lose its Market or Hangar. Check on each departure and arrival. Skip the stop if the route still has 2+ valid stations. Otherwise stop at the nearest valid station and flag the route in the UI. Never throw away cargo silently |
| Captain's cut | Not a real issue. Vanilla sets `cut = 0` when a captain joins the player fleet (`GameManager`), and the cut only splits credit loot picked up by *spawned* fleet ships (`CargoSystem`). Route profits go straight to the player. Add a cut later only as an optional balance setting |
| Market saturation | NPC prices react to stock. Several ships delivering the same good can crash its price, and "highest paying station" ranking then sends every ship to the same place. Rank on the price *after* this ship's planned delivery, and reserve a delivery when a ship departs, so ships in flight don't all target one station |
| Permadeath | Permadeath saves have one save file, no manual save and no reload (`MenuControl`). Losing a route ship or its captain in combat is final, which fits. The stale-sidecar check matters less here because there's no reload, but a crash between the vanilla save and the sidecar write can still leave them out of step, so keep the check |

## Suggested phasing

1. **Core simulation, no visuals (~40% of effort).** Sidecar persistence, the route
   model, the gate-graph BFS, the abstract travel and trade tick, fleet-logic
   exclusion patches, and the Fleet Commander exemption. A temporary debug panel (or BepInEx config) creates routes, which
   proves the economics loop.
2. **Station UI (~25%).** The "Assign to route" button, the Trade Routes docking tab
   (ship list, unassign, per-route goods).
3. **Galaxy-map UI (~20%).** The station picker panel, the route editor with
   add/remove/split, and route lines.
4. **Danger and presence (~15%).** Split in two after research showed spawning route ships
   needs heavy patching (an `AIMercenary` with no guard target warps away by itself, and only
   counts as the player's ship on death while guarding the player; `AITraderControl` is driven
   by the game's off-screen trader records):
   - **4a, done: off-screen danger.** Route ships crossing a sector where a faction hostile to
     the player is fighting (`TSector.attackers`) may be hit. A kill means an emergency warp
     if the ship has a warp drive (as vanilla fleet ships do at death), otherwise the ship and
     cargo are lost and the captain and crew return to the station it last left. Damage is repaired in full at the next
     stop: free at the player's stations, the game's repair price at NPC stations, and if the
     player can't pay, repaired anyway for what they have.
   - **4b, later: visible ships.** Spawn route ships in the player's sector, fly them to the
     station or gate, then despawn, with real combat.
5. **Optional.** A real "Trading" `StationModule`, off-screen risk, and the 12
   localisation files like the siblings.

Phase 1 alone answers the riskiest question: whether an abstract trader can run cleanly
alongside the vanilla fleet code. Prototyping it first is recommended.

## Reference: key game classes

All in the decompiled `Assembly-CSharp`:

| Area | Classes and members |
|---|---|
| Stations | `Station` (`id`, `ownerID`, `sectorIndex`, `x/y`, `modules`, `itemStock`, `IsPlayerFriendly`, `GetMarketPriceForItem`, `SellItemToNPC`, `BuyItemFromNPC`); `GameData.GetStation`; `GameDataInfo.stationList`; `TSector.stationIDs` |
| Modules | `StationModule`, `SM_DB` (registry, `Validate`), `SM_Market` (id 50), `SM_Hangar` (id 40), `SM_CargoTransfer` (id 105), `DynamicMarketItem` (`tradeMode`, `targetStock`), `StationModuleConfig`, `StationManagement` |
| Fleet | `PChar.Char.mercenaries`, `PlayerFleetMember`, `AIMercenaryCharacter` (`dockedStationID`, `sectorIndex`, `IsActive`), `SpaceShipData` (`cargo`, `members`), `Inventory.AddToFleet`, `GameManager.SpawnMercenary` / `SpawnPlayerFleet` / `LaunchPlayerFleetMember`, `AIMercenary.EmergencyWarp` / `Die` |
| Off-sector simulation | `GameDataInfo.UpdatesAsync` (Stations Thread), `CharacterSystem.UpdateCharacters`, `DynamicCharacter.UpdateCharacter` (off-screen NPC trading template), `AITraderControl` (in-sector trading template), `GameData.timePlayed`, `GameData.threadSaveLock` |
| Sectors | `GameDataInfo.sectors`, `TSector.jumpGates` (`connectedSector`, `discovered`), `JumpgateStation.connectedJumpgateStation`, `SM_JumpGateGenerator` |
| Save | `GameData.SaveGame` / `GetGameData` / `SetGameData` / `NewGame`, `GameData.gameFileIndex`, `MenuControl.SaveGame` / `LoadGame` / `DeleteSaveGame` |
| UI | `GalaxyMap` (`OnSectorSelect`, `SelectSector`, `selCanvas`, `BlockMapMovement`), `DockingUI` (`OpenPanel`, `ShowHideDockingButtons`), `Inventory.SelectItem` (`cargoMode == 2`), `AssignCaptainDialog` |
