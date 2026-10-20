using System.Collections.Generic;
using System.Runtime.Serialization;

namespace StationTransport
{
    // Saved with DataContractJsonSerializer (see RouteJson). Every member is optional, so a file
    // written before a field existed still loads, and fields it doesn't know are ignored.
    // Deserializing skips constructors and field initializers, so each class sets its defaults
    // in an [OnDeserializing] method as well as in its constructor.

    /// <summary>Everything the mod saves, written to a sidecar file next to the vanilla save.</summary>
    [DataContract]
    public class RouteSaveData
    {
        [DataMember] public int version;

        /// <summary>
        /// <see cref="GameDataInfo.totalTimePlayed"/> when this file was written. On load it must
        /// match the vanilla save, or the file belongs to a different save point.
        /// </summary>
        [DataMember] public float stamp;

        [DataMember] public int nextRouteId;
        [DataMember] public List<TradeRoute> routes;
        [DataMember] public List<RouteShip> ships;

        public RouteSaveData()
        {
            SetDefaults();
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            SetDefaults();
        }

        private void SetDefaults()
        {
            version = 1;
            nextRouteId = 1;
            routes = new List<TradeRoute>();
            ships = new List<RouteShip>();
        }
    }

    [DataContract]
    public class TradeRoute
    {
        [DataMember] public int id;

        /// <summary>The default name in the language it was created in; shown names use <see cref="DisplayName"/>.</summary>
        [DataMember] public string name;

        /// <summary>A name the player gave the route, or null for the default.</summary>
        [DataMember] public string customName;

        /// <summary>The player's name for the route, or "Route 3" in the current game language.</summary>
        public string DisplayName => string.IsNullOrEmpty(customName) ? DefaultName : customName;

        public string DefaultName => Loc.Get(Loc.RouteName, id);

        /// <summary>Stops in order, by <see cref="Station.id"/>.</summary>
        [DataMember] public List<int> stations;

        /// <summary>
        /// A loop runs last stop → first stop. Otherwise ships reverse at each end.
        /// </summary>
        [DataMember] public bool circular;

        public TradeRoute()
        {
            SetDefaults();
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            SetDefaults();
        }

        private void SetDefaults()
        {
            stations = new List<int>();
        }
    }

    public enum ShipState
    {
        /// <summary>Docked at <see cref="RouteShip.stationId"/>, trading or waiting to leave.</summary>
        Docked = 0,

        /// <summary>Flying from <see cref="RouteShip.stationId"/> to <see cref="RouteShip.targetStationId"/>.</summary>
        Transit = 1,
    }

    public enum StuckReason
    {
        None = 0,

        /// <summary>No chain of jump gates leads to any other usable stop.</summary>
        NoGateRoute = 1,

        /// <summary>The route has fewer than two stops a ship can use.</summary>
        TooFewStops = 2,
    }

    /// <summary>A fleet ship the mod controls: either on a route, or in reserve.</summary>
    [DataContract]
    public class RouteShip
    {
        /// <summary>The ship's captain, which identifies a <see cref="PlayerFleetMember"/>.</summary>
        [DataMember] public int crewMemberID;

        /// <summary>
        /// The route the ship flies, or <see cref="Reserve"/>: unassigned, but kept out of the
        /// active fleet because there was no Fleet Commander room for it.
        /// </summary>
        [DataMember] public int routeId;

        [DataMember] public ShipState state;

        /// <summary>Docked: where the ship is. Transit: the station it left.</summary>
        [DataMember] public int stationId;

        /// <summary>Transit: where the ship is going.</summary>
        [DataMember] public int targetStationId;

        /// <summary>The index in the route of the stop the ship is at or flying to, or -1.</summary>
        [DataMember] public int stopIndex;

        /// <summary>+1 or -1 along the route. Only changes on routes that aren't loops.</summary>
        [DataMember] public int direction;

        /// <summary>Transit: departure time, in <see cref="GameData.timePlayed"/> seconds.</summary>
        [DataMember] public float departTime;

        /// <summary>Docked: when the ship may leave. Transit: arrival time.</summary>
        [DataMember] public float eventTime;

        /// <summary>Docked: the ship arrived and hasn't traded yet.</summary>
        [DataMember] public bool tradePending;

        /// <summary>The player unassigned the ship in flight; it stops at its destination.</summary>
        [DataMember] public bool unassignPending;

        /// <summary>
        /// Hull damage from off-screen attacks, 0 (none) to 1 (destroyed). Kept here rather than in
        /// the ship's own record, whose HP fields mean different things to the spawn code.
        /// Repaired at the next stop.
        /// </summary>
        [DataMember] public float damage;

        /// <summary>Transit: the ship emergency-warped out of a fight and arrives late.</summary>
        [DataMember] public bool emergencyWarped;

        /// <summary>
        /// Why the ship can't move on, or <see cref="StuckReason.None"/>. Kept so the status line
        /// doesn't redo the gate search, and so the player is told once, not at every retry.
        /// </summary>
        [DataMember] public StuckReason stuck;

        public const int Reserve = 0;

        public bool InReserve => routeId == Reserve;

        public RouteShip()
        {
            SetDefaults();
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            SetDefaults();
        }

        private void SetDefaults()
        {
            crewMemberID = -1;
            stationId = -1;
            targetStationId = -1;
            stopIndex = -1;
            direction = 1;
        }
    }
}
