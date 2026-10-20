namespace StationTransport
{
    /// <summary>Which stations a route ship may trade at.</summary>
    internal static class RouteStations
    {
        /// <summary>
        /// The station can be on a route: a player station with a Market and a Hangar, or a
        /// friendly NPC station with a Market. Adding it to a route is all the opting in it
        /// needs; its Market settings decide which goods move.
        /// </summary>
        public static bool CanJoinRoute(Station station)
        {
            if (station == null || station.DestroyedOrWrecked || !station.hasMarket || station is JumpgateStation)
            {
                return false;
            }
            if (station.PlayerOwned)
            {
                return station.HasDocking(broadcastingRequired: false);
            }
            return station.discovered && station.IsPlayerFriendly;
        }

        /// <summary>A route ship can dock and trade at the station right now.</summary>
        public static bool IsUsable(Station station)
        {
            return CanJoinRoute(station) && station.InActivity;
        }

        public static Station Get(int id)
        {
            return id < 0 ? null : GameData.GetStation(id, allowDestroyedOrWrecked: false);
        }

        public static string Name(int id)
        {
            Station station = GameData.GetStation(id, allowDestroyedOrWrecked: true);
            if (station == null)
            {
                return Loc.Get(Loc.StationGone);
            }
            return station.stationName(withLevel: false) + " " + station.Sector.coords;
        }
    }
}
