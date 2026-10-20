using System.Collections.Generic;

namespace StationTransport
{
    /// <summary>
    /// Changes to a route's stops. Keeps the ships on it pointing at the right stop. A route is a
    /// loop when it has been closed back to its first stop; otherwise ships go there and back.
    /// </summary>
    internal static class RouteEditor
    {
        /// <summary>Raised after any change, so the map can redraw.</summary>
        public static event System.Action Changed = delegate { };

        public static TradeRoute NewRoute(int firstStation)
        {
            return CreateRoute(new List<int> { firstStation }, circular: false);
        }

        public static TradeRoute CreateRoute(List<int> stations, bool circular)
        {
            TradeRoute route = RouteStore.AddRoute(stations, circular && stations.Count >= MinLoopStops);
            TradeLog.Add($"{route.DisplayName} created with {route.stations.Count} stop(s)");
            Changed();
            return route;
        }

        /// <summary>Adds a stop at the end. A station can only be on a route once.</summary>
        public static bool AddStop(TradeRoute route, int stationId)
        {
            if (route.stations.Contains(stationId))
            {
                return false;
            }
            route.stations.Add(stationId);
            Reindex(route);
            Changed();
            return true;
        }

        public static void MoveUp(TradeRoute route, int index)
        {
            if (index <= 0 || index >= route.stations.Count)
            {
                return;
            }
            (route.stations[index - 1], route.stations[index]) = (route.stations[index], route.stations[index - 1]);
            Reindex(route);
            Changed();
        }

        public const int MaxNameLength = 32;

        /// <summary>
        /// Names the route. Blank, or the default name, goes back to the default, which follows the
        /// game language. Angle brackets are dropped so a name can't inject rich-text tags.
        /// </summary>
        public static void Rename(TradeRoute route, string newName)
        {
            string clean = (newName ?? "").Replace("<", "").Replace(">", "").Trim();
            if (clean.Length > MaxNameLength)
            {
                clean = clean.Substring(0, MaxNameLength).Trim();
            }
            string custom = clean.Length == 0 || clean == route.DefaultName ? null : clean;
            if (custom == route.customName)
            {
                return;
            }
            string old = route.DisplayName;
            route.customName = custom;
            TradeLog.Add($"{old} renamed to {route.DisplayName}");
            Changed();
        }

        /// <summary>
        /// A loop needs three stops: with two, going round and going there and back are the same.
        /// </summary>
        public const int MinLoopStops = 3;

        public static bool CanCloseLoop(TradeRoute route)
        {
            return !route.circular && route.stations.Count >= MinLoopStops;
        }

        /// <summary>Ships go from the last stop back to the first, round and round.</summary>
        public static void CloseLoop(TradeRoute route)
        {
            if (CanCloseLoop(route))
            {
                route.circular = true;
                TradeLog.Add($"{route.DisplayName} now loops back to {RouteStations.Name(route.stations[0])}");
                Changed();
            }
        }

        /// <summary>Ships go to the last stop, then back the same way.</summary>
        public static void OpenLoop(TradeRoute route)
        {
            if (route.circular)
            {
                route.circular = false;
                TradeLog.Add($"{route.DisplayName} no longer loops");
                Changed();
            }
        }

        /// <summary>
        /// Takes a stop out and joins its neighbours. A loop stays a loop while it has enough
        /// stops; removing its first stop makes the next one the start.
        /// </summary>
        public static void RemoveStop(TradeRoute route, int index)
        {
            if (index < 0 || index >= route.stations.Count)
            {
                return;
            }
            int removed = route.stations[index];
            route.stations.RemoveAt(index);
            if (route.stations.Count < MinLoopStops)
            {
                route.circular = false;
            }
            TradeLog.Add($"{RouteStations.Name(removed)} removed from {route.DisplayName}");
            Reindex(route);
            if (route.stations.Count == 0)
            {
                DeleteRoute(route);
            }
            Changed();
        }

        /// <summary>Only a stop between the ends of a route that isn't a loop can split it.</summary>
        public static bool CanSplitAt(TradeRoute route, int index)
        {
            return !route.circular && index > 0 && index < route.stations.Count - 1;
        }

        /// <summary>
        /// Removes a stop and breaks the route in two there. A half with fewer than two stops
        /// can't run, so it's dropped, and every ship stays with the half that's left. With two
        /// usable halves, the stops after the split become a new route, and the ships docked at
        /// or flying to them go with it. Returns the new route, or null if there isn't one.
        /// </summary>
        public static TradeRoute SplitAt(TradeRoute route, int index)
        {
            if (!CanSplitAt(route, index))
            {
                return null;
            }
            int removed = route.stations[index];
            List<int> head = route.stations.GetRange(0, index);
            List<int> tail = route.stations.GetRange(index + 1, route.stations.Count - index - 1);
            bool headRuns = head.Count >= 2;
            bool tailRuns = tail.Count >= 2;
            TradeRoute newRoute = null;
            if (headRuns && tailRuns)
            {
                route.stations = head;
                newRoute = RouteStore.AddRoute(tail, circular: false);
                foreach (RouteShip ship in RouteStore.Ships)
                {
                    if (ship.routeId == route.id && tail.Contains(Position(ship)))
                    {
                        ship.routeId = newRoute.id;
                    }
                }
                Reindex(newRoute);
                TradeLog.Add($"{route.DisplayName} split at {RouteStations.Name(removed)}: the stops after it are now {newRoute.DisplayName}");
            }
            else if (headRuns || tailRuns)
            {
                route.stations = headRuns ? head : tail;
                TradeLog.Add($"{route.DisplayName} split at {RouteStations.Name(removed)}; the one-stop end was dropped");
            }
            else
            {
                TradeLog.Add($"{route.DisplayName} split at {RouteStations.Name(removed)}; neither end has two stops");
                DeleteRoute(route);
                Changed();
                return null;
            }
            Reindex(route);
            Changed();
            return newRoute;
        }

        /// <summary>Ships on the route leave it: docked ones now, ones in flight at their next stop.</summary>
        public static void DeleteRoute(TradeRoute route)
        {
            foreach (RouteShip ship in RouteStore.Ships.ToArray())
            {
                if (ship.routeId == route.id)
                {
                    RouteSim.Unassign(ship);
                }
            }
            RouteStore.Routes.Remove(route);
            TradeLog.Add($"{route.DisplayName} deleted");
            Changed();
        }

        /// <summary>The station a ship is at, or flying to.</summary>
        private static int Position(RouteShip ship)
        {
            return ship.state == ShipState.Transit ? ship.targetStationId : ship.stationId;
        }

        /// <summary>
        /// Points each ship's stop index at its station's new place in the route. A ship whose stop
        /// was removed gets -1, and goes to the nearest stop after its next dock.
        /// </summary>
        private static void Reindex(TradeRoute route)
        {
            foreach (RouteShip ship in RouteStore.Ships)
            {
                if (ship.routeId == route.id)
                {
                    ship.stopIndex = route.stations.IndexOf(Position(ship));
                }
            }
        }
    }
}
