using System.Collections.Generic;
using UnityEngine;

namespace StationTransport
{
    /// <summary>
    /// Off-screen danger to route ships, and their repairs. The game fights battles in sectors
    /// it isn't showing (TSector.attackers); a route ship that flies through one fought by a
    /// faction hostile to the player may be hit. Shot down, it emergency-warps out if it has a
    /// warp drive, as the game's own fleet ships do; otherwise it's lost and its captain and crew
    /// come home. Every damaged ship is repaired in full at its next stop.
    /// </summary>
    internal static class Hazards
    {
        /// <summary>Damage from one hit, as a share of the hull. Above 1 is a kill on its own.</summary>
        private const float MinHit = 0.15f;
        private const float MaxHit = 1.3f;

        /// <summary>What's left of the hull after an emergency warp, for the repair bill.</summary>
        private const float DamageAfterWarp = 0.95f;

        /// <summary>Repairs cost this much per missing hull point, as Station.GetRepairPrice.</summary>
        private const float RepairPricePerHull = 1.5f;
        private const float RepairDiscountPerRepRank = 0.1f;

        private const int CrewItem = 5;

        public enum Outcome
        {
            Safe,
            EmergencyWarp,
            Destroyed,
        }

        /// <summary>Rolls for each hostile fight on the leg the ship just flew.</summary>
        public static Outcome RollLeg(RouteShip ship, PlayerFleetMember member)
        {
            if (!Plugin.OffscreenRisk.Value)
            {
                return Outcome.Safe;
            }
            Station from = GameData.GetStation(ship.stationId, allowDestroyedOrWrecked: true);
            Station to = GameData.GetStation(ship.targetStationId, allowDestroyedOrWrecked: true);
            if (from == null || to == null)
            {
                return Outcome.Safe;
            }
            List<int> path = GateGraph.SectorPath(from.sectorIndex, to.sectorIndex) ?? new List<int> { to.sectorIndex };
            foreach (int index in path)
            {
                TSector sector = GameData.data.sectors[index];
                if (!HostileFight(sector) || Random.value >= Plugin.RiskChance.Value)
                {
                    continue;
                }
                ship.damage += Random.Range(MinHit, MaxHit);
                if (ship.damage < 1f)
                {
                    TradeLog.Add($"{Name(member)} was attacked in {sector.coords}; hull at {(1f - ship.damage) * 100f:0}%");
                    continue;
                }
                if (member.HasWarpDrive())
                {
                    ship.damage = DamageAfterWarp;
                    Announce(Loc.Get(Loc.EmergencyWarped, Name(member), sector.coords), important: false);
                    return Outcome.EmergencyWarp;
                }
                Destroy(ship, member, sector);
                return Outcome.Destroyed;
            }
            return Outcome.Safe;
        }

        /// <summary>A faction hostile to the player is fighting in the sector.</summary>
        private static bool HostileFight(TSector sector)
        {
            if (!sector.IsBeingAttacked)
            {
                return false;
            }
            foreach (AttackOnSector attack in sector.attackers)
            {
                if (attack != null && HostileToPlayer(attack.factionIndex))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The inverse of how Station.IsPlayerFriendly judges a faction.</summary>
        private static bool HostileToPlayer(int factionIndex)
        {
            return factionIndex != 0 ? PChar.GetRepRank(factionIndex) < 0 : PChar.IsMarauder;
        }

        /// <summary>
        /// The ship and its cargo are lost. The captain and the rest of the crew survive and go
        /// back to the station the ship last left, or one of the player's stations if that one is
        /// gone, the way the game returns crew when a ship's crew is unassigned (ShipInfo).
        /// </summary>
        private static void Destroy(RouteShip ship, PlayerFleetMember member, TSector sector)
        {
            Station home = CrewHome(ship);
            int stationId = home != null ? home.id : -1;
            CargoSystem cs = PlayerControl.inst.GetCargoSystem;
            CrewMember captain = CrewDB.GetCrewMember(member.crewMemberID);
            var saved = new HashSet<int>();
            if (captain != null)
            {
                captain.aiChar.behavior = member.behavior;
                cs.StoreItem(CrewItem, captain.id, captain.rarity, 1, 0f, stationId, -1);
                saved.Add(captain.id);
            }
            List<AssignedCrewMember> crew = member.shipData?.members;
            if (crew != null)
            {
                // One person can fill more than one post, so return each only once.
                foreach (AssignedCrewMember post in crew)
                {
                    CrewMember person = CrewDB.GetCrewMember(post.crewMemberID);
                    if (person != null && saved.Add(person.id))
                    {
                        cs.StoreItem(CrewItem, person.id, person.rarity, 1, 0f, stationId, -1);
                    }
                }
                crew.Clear();
            }
            if (FleetControl.instance != null)
            {
                FleetControl.instance.CleanFleetSlot(member);
            }
            PChar.Char.mercenaries.Remove(member);
            RouteStore.RemoveShip(ship);
            string where = home != null ? home.stationName(withLevel: false) : Loc.Get(Loc.YourShip);
            Announce(Loc.Get(Loc.ShipLost, Name(member), sector.coords, where, saved.Count), important: true);
            if (FleetControl.instance != null)
            {
                FleetControl.instance.Refresh(forceOpen: false);
            }
            if (Inventory.instance != null)
            {
                Inventory.instance.RefreshIfOpen(null, resetScreen: true, alsoUpdateShipInfo: false);
            }
        }

        private static Station CrewHome(RouteShip ship)
        {
            Station home = GameData.GetStation(ship.stationId, allowDestroyedOrWrecked: false);
            if (home != null)
            {
                return home;
            }
            foreach (Station station in GameData.data.stationList)
            {
                if (station.PlayerOwned && !station.DestroyedOrWrecked && station.HasDocking(broadcastingRequired: false))
                {
                    return station;
                }
            }
            return null;
        }

        /// <summary>
        /// Repairs the ship in full. NPC stations charge what they charge for repairs; the player's
        /// own stations repair free, as they do the player's ship. If the player can't cover the
        /// bill, the ship is repaired anyway for whatever they have.
        /// </summary>
        public static void Repair(RouteShip ship, PlayerFleetMember member, Station station)
        {
            if (ship.damage <= 0f)
            {
                return;
            }
            float price = 0f;
            if (!station.PlayerOwned)
            {
                float discount = PChar.GetRepRank(station.factionIndex) * RepairDiscountPerRepRank;
                price = Mathf.Max(0f, RepairPricePerHull * ship.damage * ShipEstimates.HullPoints(member) * (1f - discount));
            }
            CargoSystem wallet = PlayerControl.inst.GetCargoSystem;
            float paid = Mathf.Min(price, Mathf.Max(0f, wallet.credits));
            if (paid > 0f && wallet.PayCreditCost(paid, showErrorMsg: false))
            {
                station.AddCredits(paid);
            }
            ship.damage = 0f;
            member.emergencyWarpReady = true;
            if (paid < price)
            {
                Announce(Loc.Get(Loc.RepairShort, Name(member), station.stationName(withLevel: false), paid.ToString("N0"), price.ToString("N0")), important: false);
            }
            else
            {
                TradeLog.Add($"{Name(member)} repaired at {station.stationName(withLevel: false)} for {paid:N0}");
            }
        }

        private static void Announce(string message, bool important)
        {
            TradeLog.Add(message);
            SideInfo.AddMsg((important ? ColorSys.infoNeg : ColorSys.infoNeg2) + message + "</color>");
            if (important && InfoPanelControl.inst != null)
            {
                InfoPanelControl.inst.ShowWarning(message, 6, playAudio: false);
            }
        }

        private static string Name(PlayerFleetMember member)
        {
            return member.CommanderName(14, colored: false);
        }
    }
}
