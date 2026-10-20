# Star Valor Mod to allow making trade routes between stations

Requires the station building DLC

A mod which allows a player to pick a ship in their player owned station 
which is not part of their current active fleet and deploy that ship to a trade route

Trade routes are defined between player owned stations and other player owned or friendly 
stations. Trade routes may traverse multiple stations. A ship arriving at the end of a 
non-circular trade route will reverse course and return along the same route. Trading player
stations must have a "Market", a "Landing Dock" and the new "Trading" module, NPC stations are assume to have
those. Multiple ships may be assigned to the same trade route. Multiple stations in a trade
route may be in the same sector or different sectors. Multiple trade routes may
include the same station, but the ships will only fly the route to which they
are assigned.

New trading station module functions like "market" module with upgrades and
a similar provide/request scheme

Sectors in a trade route must be linked by warp gates! This restriction 
actually makes warp gates worth building. Warp engines are not required on
trade ships and make no difference if equipped except for the usual 
emergency warp capability. Ships which emergency warp out will return and 
attempt to continue once repaired

Each trade route has a set of provided and requested goods at each station
Ships will attempt to deliver goods and load provided goods if any other
station on their trade route is requesting them. Ships will deliver goods
to the highest paying station for that good on their trade route, up to
the requested good stock limit, then the next highest paying etc.

Trade ships travel even if the player's current sector does not 
contain any of their trade route stations

Ships whose trading station demands/supply change whilst they are in flight
will deliver their no longer required cargo items to the next available station
if no station on the trade route is demanding them

Trade ships travel at their rated engine speed like any other ship. Loading
and unloading cargo takes a configurable time during which the ship is docked
in the station

New UI required - On star map select an already discovered station and start
or add it to a trade route, also optionally remove a station from a trade route. 
Adding a station requires being able to select from possibly multiple stations
in a map sector.
Removing a station from the middle of a trade route will split the trade route
 into two other trade routes, unless it is a circular route

In station assign idle ship to a trade route - if the trade route it is assigned
to does not touch that station then the ship flies to the nearest station which 
*is* on the trade route before beginning to trade. If ship is assigned to a trade
route then a list of assigned ships will be shown on a new panel and a ship may
be unassigned in which case it will stop at the next station it is in flight to.

Assigning a ship to a trade route requires equipment and crew as usual and a Captain
to be appointed at the time the ship is assigned to the trade route

Ships assigned to a trade route do not count against the player's Fleet Commander
limit

## Open Questions

* How will these new features be recorded in save files
* What UI buttons and panels are needed
* Are there any UI gaps which need to be addressed
* Does it make sense to have a separate trading module or use the existing market module
* Should player stations which are linked to factors requiring certain goods auto-request them
