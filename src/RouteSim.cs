using System;
using System.Threading;
using UnityEngine;

namespace StationTransport
{
    /// <summary>
    /// The abstract travel simulation. Route ships are never spawned: each one is a record that
    /// is either docked at a stop or in transit, timed by <see cref="GameData.timePlayed"/>,
    /// which stops while the game is paused and is saved with the game.
    ///
    /// While a ship is in transit its vanilla record says it is away (dockedStationID -1, in the
    /// destination's sector), so the game's own checks treat it as not at any station. Docked,
    /// it is a normal docked fleet ship.
    /// </summary>
    internal static class RouteSim
    {
        /// <summary>Wait before retrying when a ship has nowhere to go.</summary>
        private const float RetrySeconds = 30f;

        private const float CleanupInterval = 2f;

        /// <summary>Set while the game writes a save; route state must not change under it.</summary>
        public static bool Saving;

        private static float nextCleanup;

        /// <summary>A game is loaded and the mod may run in it.</summary>
        public static bool GameActive
        {
            get
            {
                GameManager manager = GameManager.instance;
                return manager != null && manager.inGame && GameData.data != null && PChar.Char != null
                    && PlayerControl.inst != null && Plugin.AllowedIn(GameData.data);
            }
        }

        public static void Tick()
        {
            if (Saving || !GameActive)
            {
                return;
            }
            TradeLog.Tick.Start();
            float now = GameData.timePlayed;
            if (now >= nextCleanup)
            {
                nextCleanup = now + CleanupInterval;
                DropLostShips();
            }
            var ships = RouteStore.Ships;
            for (int i = ships.Count - 1; i >= 0; i--)
            {
                RouteShip ship = ships[i];
                if (ship.InReserve || (now < ship.eventTime && !ship.tradePending))
                {
                    continue;
                }
                PlayerFleetMember member = RouteStore.FindMember(ship);
                if (member != null)
                {
                    Step(ship, member, now);
                }
            }
            TradeLog.Tick.End();
        }

        private static void Step(RouteShip ship, PlayerFleetMember member, float now)
        {
            if (ship.state == ShipState.Transit)
            {
                Arrive(ship, member, now);
                return;
            }
            if (ship.tradePending)
            {
                if (TryTrade(ship, member))
                {
                    ship.tradePending = false;
                    ship.eventTime = now + Plugin.DockSeconds.Value;
                }
                return;
            }
            if (ship.unassignPending)
            {
                Release(ship, member);
                return;
            }
            Depart(ship, member, now);
        }

        /// <summary>Trades at the ship's stop. False if the stock lock was busy; try next frame.</summary>
        private static bool TryTrade(RouteShip ship, PlayerFleetMember member)
        {
            Station here = RouteStations.Get(ship.stationId);
            TradeRoute route = RouteStore.GetRoute(ship.routeId);
            if (route == null || !RouteStations.IsUsable(here))
            {
                return true;
            }
            // Never block: the Stations Thread can hold this lock for hundreds of milliseconds.
            if (!Monitor.TryEnter(GameData.threadSaveLock))
            {
                TradeLog.LockMisses++;
                return false;
            }
            try
            {
                TradeLog.Stop.Start();
                Trader.TradeAt(member, ship, route, here);
                TradeLog.Stop.End();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Trade at {RouteStations.Name(ship.stationId)} failed: {e}");
            }
            finally
            {
                Monitor.Exit(GameData.threadSaveLock);
            }
            return true;
        }

        private static void Depart(RouteShip ship, PlayerFleetMember member, float now)
        {
            TradeRoute route = RouteStore.GetRoute(ship.routeId);
            if (route == null)
            {
                Release(ship, member);
                return;
            }
            Station from = GameData.GetStation(ship.stationId, allowDestroyedOrWrecked: true);
            if (!PlanNextStop(ship, route, from, ShipEstimates.Speed(member), now))
            {
                ship.eventTime = now + RetrySeconds;
                ReportStuck(ship, member, route, from);
                return;
            }
            ClearStuck(ship);
            member.dockedStationID = -1;
            member.sectorIndex = RouteStations.Get(ship.targetStationId).sectorIndex;
            TradeLog.Add($"{Name(member)} left {RouteStations.Name(ship.stationId)} for {RouteStations.Name(ship.targetStationId)}, ETA {ship.eventTime - now:0}s");
            RefreshFleetPanel();
        }

        private static void Arrive(RouteShip ship, PlayerFleetMember member, float now)
        {
            if (ship.emergencyWarped)
            {
                // Already rolled for this leg; the warp only made it late.
                ship.emergencyWarped = false;
            }
            else
            {
                switch (Hazards.RollLeg(ship, member))
                {
                    case Hazards.Outcome.Destroyed:
                        return;
                    case Hazards.Outcome.EmergencyWarp:
                        ship.emergencyWarped = true;
                        ship.eventTime = now + Plugin.EmergencyWarpDelaySeconds.Value;
                        return;
                }
            }
            Station to = RouteStations.Get(ship.targetStationId);
            bool canDock = ship.unassignPending ? RouteStations.CanJoinRoute(to) : RouteStations.IsUsable(to);
            if (!canDock)
            {
                // The stop was lost on the way (destroyed, turned hostile, switched off): carry on
                // from there.
                Station lost = GameData.GetStation(ship.targetStationId, allowDestroyedOrWrecked: true);
                TradeRoute route = RouteStore.GetRoute(ship.routeId);
                int origin = ship.stationId;
                ship.stationId = ship.targetStationId;
                if (route != null && lost != null && PlanNextStop(ship, route, lost, ShipEstimates.Speed(member), now))
                {
                    member.sectorIndex = RouteStations.Get(ship.targetStationId).sectorIndex;
                    TradeLog.Add($"{Name(member)} could not dock at {RouteStations.Name(ship.stationId)}; going on to {RouteStations.Name(ship.targetStationId)}");
                }
                else
                {
                    ship.stationId = origin;
                    ship.eventTime = now + RetrySeconds;
                    if (route != null)
                    {
                        ReportStuck(ship, member, route, lost);
                    }
                }
                return;
            }
            ClearStuck(ship);
            ship.state = ShipState.Docked;
            ship.stationId = to.id;
            ship.targetStationId = -1;
            ship.tradePending = !ship.unassignPending;
            ship.eventTime = now;
            member.dockedStationID = to.id;
            member.sectorIndex = to.sectorIndex;
            TradeLog.Add($"{Name(member)} docked at {RouteStations.Name(to.id)}");
            Hazards.Repair(ship, member, to);
            RefreshFleetPanel();
        }

        /// <summary>
        /// Records why the ship can't move on, and tells the player once rather than at every retry.
        /// </summary>
        private static void ReportStuck(RouteShip ship, PlayerFleetMember member, TradeRoute route, Station from)
        {
            int otherUsable = 0;
            foreach (int id in route.stations)
            {
                if ((from == null || id != from.id) && RouteStations.IsUsable(RouteStations.Get(id)))
                {
                    otherUsable++;
                }
            }
            StuckReason reason = otherUsable == 0 ? StuckReason.TooFewStops : StuckReason.NoGateRoute;
            if (ship.stuck == reason)
            {
                return;
            }
            ship.stuck = reason;
            string message = Loc.Get(reason == StuckReason.NoGateRoute ? Loc.StuckNoGate : Loc.StuckTooFewStops, Name(member), route.DisplayName);
            TradeLog.Add(message);
            SideInfo.AddMsg(ColorSys.infoNeg2 + message + "</color>");
        }

        private static void ClearStuck(RouteShip ship)
        {
            ship.stuck = StuckReason.None;
        }

        /// <summary>
        /// Picks the next stop and sets the ship in transit to it. Ships at a stop on their route go
        /// on along it; ships elsewhere go to the nearest stop first.
        /// </summary>
        private static bool PlanNextStop(RouteShip ship, TradeRoute route, Station from, float speed, float now)
        {
            int count = route.stations.Count;
            if (from == null || count < 2)
            {
                return false;
            }
            int index = -1;
            int direction = ship.direction == 0 ? 1 : ship.direction;
            float time = -1f;
            bool onRoute = ship.stopIndex >= 0 && ship.stopIndex < count && route.stations[ship.stopIndex] == from.id;
            if (onRoute)
            {
                int candidate = ship.stopIndex;
                for (int step = 0; step < count * 2; step++)
                {
                    candidate = Advance(route, candidate, ref direction);
                    int id = route.stations[candidate];
                    if (id == from.id || !RouteStations.IsUsable(RouteStations.Get(id)))
                    {
                        continue;
                    }
                    time = GateGraph.TravelTime(from, RouteStations.Get(id), speed);
                    if (time >= 0f)
                    {
                        index = candidate;
                        break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    Station stop = RouteStations.Get(route.stations[i]);
                    if (stop == from || !RouteStations.IsUsable(stop))
                    {
                        continue;
                    }
                    float t = GateGraph.TravelTime(from, stop, speed);
                    if (t >= 0f && (index < 0 || t < time))
                    {
                        index = i;
                        time = t;
                    }
                }
                direction = !route.circular && index == count - 1 ? -1 : 1;
            }
            if (index < 0)
            {
                return false;
            }
            ship.state = ShipState.Transit;
            ship.targetStationId = route.stations[index];
            ship.stopIndex = index;
            ship.direction = direction;
            ship.departTime = now;
            ship.eventTime = now + time;
            return true;
        }

        /// <summary>The next index along the route, turning round at the ends of a route that isn't circular.</summary>
        private static int Advance(TradeRoute route, int index, ref int direction)
        {
            int count = route.stations.Count;
            if (route.circular)
            {
                return ((index + direction) % count + count) % count;
            }
            int next = index + direction;
            if (next < 0 || next >= count)
            {
                direction = -direction;
                next = index + direction;
            }
            return Mathf.Clamp(next, 0, count - 1);
        }

        public static bool CanAssign(PlayerFleetMember member)
        {
            RouteShip existing = RouteStore.Get(member);
            return member.alive && member.dockedStationID >= 0 && !member.hangarDocked && (existing == null || existing.InReserve);
        }

        public static void Assign(PlayerFleetMember member, TradeRoute route)
        {
            if (!CanAssign(member))
            {
                return;
            }
            RouteShip ship = RouteStore.Get(member) ?? RouteStore.AddShip(member, route.id);
            ship.routeId = route.id;
            ship.state = ShipState.Docked;
            ship.stationId = member.dockedStationID;
            ship.stopIndex = route.stations.IndexOf(ship.stationId);
            ship.direction = 1;
            ship.tradePending = true;
            ship.unassignPending = false;
            ship.stuck = StuckReason.None;
            ship.eventTime = GameData.timePlayed;
            TradeLog.Add($"{Name(member)} assigned to {route.DisplayName}");
            RefreshFleetPanel();
        }

        /// <summary>A docked ship leaves its route now; a ship in flight stops at its destination.</summary>
        public static void Unassign(RouteShip ship)
        {
            PlayerFleetMember member = RouteStore.FindMember(ship);
            if (member == null)
            {
                RouteStore.RemoveShip(ship);
                return;
            }
            if (ship.state == ShipState.Transit)
            {
                ship.unassignPending = true;
                TradeLog.Add($"{Name(member)} will leave its route at {RouteStations.Name(ship.targetStationId)}");
                return;
            }
            Release(ship, member);
        }

        /// <summary>
        /// Hands a docked ship back to the normal fleet if there is Fleet Commander room for it;
        /// otherwise keeps it in reserve, still exempt but unable to launch.
        /// </summary>
        private static void Release(RouteShip ship, PlayerFleetMember member)
        {
            ship.unassignPending = false;
            ship.tradePending = false;
            bool room = !Plugin.ExemptFromFleetCommander.Value
                || PChar.GetFreeFleetSpace() >= member.ModelData().fleetSpaceOcupied;
            if (room)
            {
                RouteStore.RemoveShip(ship);
                TradeLog.Add($"{Name(member)} rejoined the fleet at {RouteStations.Name(member.dockedStationID)}");
            }
            else
            {
                ship.routeId = RouteShip.Reserve;
                TradeLog.Add($"{Name(member)} is in reserve at {RouteStations.Name(member.dockedStationID)}: no Fleet Commander room");
            }
            RefreshFleetPanel();
        }

        /// <summary>Forgets ships that left the fleet by other means (dismissed, sold, destroyed).</summary>
        private static void DropLostShips()
        {
            var ships = RouteStore.Ships;
            for (int i = ships.Count - 1; i >= 0; i--)
            {
                PlayerFleetMember member = RouteStore.FindMember(ships[i]);
                if (member == null || !member.alive)
                {
                    TradeLog.Add($"Route ship with captain #{ships[i].crewMemberID} left the fleet");
                    RouteStore.RemoveShip(ships[i]);
                }
            }
        }

        /// <summary>
        /// While a save is written, ships in flight are recorded as docked at the stop they left, so
        /// removing the mod never strands a ship that has no warp drive. <see cref="EndSave"/> undoes it.
        /// </summary>
        public static void BeginSave()
        {
            Saving = true;
            foreach (RouteShip ship in RouteStore.Ships)
            {
                PlayerFleetMember member = ship.state == ShipState.Transit ? RouteStore.FindMember(ship) : null;
                Station dock = member == null ? null : RouteStations.Get(ship.stationId) ?? RouteStations.Get(ship.targetStationId);
                if (dock != null)
                {
                    member.dockedStationID = dock.id;
                    member.sectorIndex = dock.sectorIndex;
                }
            }
        }

        public static void EndSave()
        {
            RestoreTransitRecords();
            Saving = false;
        }

        /// <summary>Puts the vanilla records of ships in flight back to "away".</summary>
        public static void RestoreTransitRecords()
        {
            foreach (RouteShip ship in RouteStore.Ships)
            {
                PlayerFleetMember member = ship.state == ShipState.Transit ? RouteStore.FindMember(ship) : null;
                if (member == null)
                {
                    continue;
                }
                Station target = GameData.GetStation(ship.targetStationId, allowDestroyedOrWrecked: true);
                member.dockedStationID = -1;
                if (target != null)
                {
                    member.sectorIndex = target.sectorIndex;
                }
            }
        }

        /// <summary>The line shown under the ship's name in the fleet panel.</summary>
        public static string Status(RouteShip ship)
        {
            if (ship.InReserve)
            {
                return Loc.Get(Loc.StatusReserve);
            }
            float now = GameData.timePlayed;
            if (ship.state == ShipState.Transit)
            {
                string damaged = ship.damage > 0f ? Loc.Get(Loc.StatusHull, ((1f - ship.damage) * 100f).ToString("0")) : "";
                return Loc.Get(ship.emergencyWarped ? Loc.StatusWarpedTo : Loc.StatusTo, StationShortName(ship.targetStationId), Clock(ship.eventTime - now)) + damaged;
            }
            if (ship.stuck != StuckReason.None)
            {
                return Loc.Get(ship.stuck == StuckReason.NoGateRoute ? Loc.StatusNoGate : Loc.StatusTooFewStops);
            }
            if (ship.unassignPending || ship.tradePending || now < ship.eventTime)
            {
                return Loc.Get(Loc.StatusTrading, StationShortName(ship.stationId));
            }
            return Loc.Get(Loc.StatusLeaving, StationShortName(ship.stationId));
        }

        private static string StationShortName(int id)
        {
            Station station = GameData.GetStation(id, allowDestroyedOrWrecked: true);
            return station == null ? "?" : station.stationName(withLevel: false);
        }

        private static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        private static string Name(PlayerFleetMember member)
        {
            return member.CommanderName(14, colored: false);
        }

        private static void RefreshFleetPanel()
        {
            if (FleetControl.instance != null)
            {
                FleetControl.instance.RefreshDelayed();
            }
        }
    }
}
