using System.Collections.Generic;

namespace StationTransport
{
    /// <summary>The mod's state for the loaded game, with lookups the patches use every frame.</summary>
    internal static class RouteStore
    {
        public static RouteSaveData Data { get; private set; } = new RouteSaveData();

        private static readonly Dictionary<int, RouteShip> shipsByCrew = new Dictionary<int, RouteShip>();

        public static List<TradeRoute> Routes => Data.routes;

        public static List<RouteShip> Ships => Data.ships;

        public static void Clear()
        {
            Set(new RouteSaveData());
        }

        public static void Set(RouteSaveData data)
        {
            Data = data ?? new RouteSaveData();
            Data.routes ??= new List<TradeRoute>();
            Data.ships ??= new List<RouteShip>();
            foreach (TradeRoute route in Data.routes)
            {
                route.stations ??= new List<int>();
            }
            Reindex();
        }

        private static void Reindex()
        {
            shipsByCrew.Clear();
            for (int i = Data.ships.Count - 1; i >= 0; i--)
            {
                RouteShip ship = Data.ships[i];
                if (ship.crewMemberID < 0 || shipsByCrew.ContainsKey(ship.crewMemberID))
                {
                    Data.ships.RemoveAt(i);
                    continue;
                }
                shipsByCrew[ship.crewMemberID] = ship;
            }
        }

        /// <summary>The mod's record for a fleet ship, or null if the mod doesn't control it.</summary>
        public static RouteShip Get(AIMercenaryCharacter character)
        {
            if (character is PlayerFleetMember member && member.crewMemberID >= 0
                && shipsByCrew.TryGetValue(member.crewMemberID, out RouteShip ship))
            {
                return ship;
            }
            return null;
        }

        /// <summary>The ship is on a route or in reserve, so it must stay out of the active fleet.</summary>
        public static bool IsControlled(AIMercenaryCharacter character)
        {
            return Get(character) != null;
        }

        public static PlayerFleetMember FindMember(RouteShip ship)
        {
            List<AIMercenaryCharacter> mercenaries = PChar.Char?.mercenaries;
            if (mercenaries == null)
            {
                return null;
            }
            foreach (AIMercenaryCharacter character in mercenaries)
            {
                if (character is PlayerFleetMember member && member.crewMemberID == ship.crewMemberID)
                {
                    return member;
                }
            }
            return null;
        }

        public static TradeRoute GetRoute(int id)
        {
            return Data.routes.Find(r => r.id == id);
        }

        public static TradeRoute AddRoute(List<int> stations, bool circular)
        {
            var route = new TradeRoute
            {
                id = Data.nextRouteId++,
                stations = new List<int>(stations),
                circular = circular,
            };
            route.name = Loc.Get(Loc.RouteName, route.id);
            Data.routes.Add(route);
            return route;
        }

        public static RouteShip AddShip(PlayerFleetMember member, int routeId)
        {
            var ship = new RouteShip
            {
                crewMemberID = member.crewMemberID,
                routeId = routeId,
                state = ShipState.Docked,
                stationId = member.dockedStationID,
                eventTime = GameData.timePlayed,
                tradePending = true,
            };
            Data.ships.Add(ship);
            shipsByCrew[ship.crewMemberID] = ship;
            return ship;
        }

        public static void RemoveShip(RouteShip ship)
        {
            Data.ships.Remove(ship);
            shipsByCrew.Remove(ship.crewMemberID);
        }
    }
}
