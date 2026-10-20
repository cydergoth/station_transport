using System.Collections.Generic;
using UnityEngine;

namespace StationTransport
{
    /// <summary>
    /// What a route ship does at a stop: deliver cargo, then load goods for the other stops.
    /// Runs on the main thread with <see cref="GameData.threadSaveLock"/> held.
    ///
    /// Goods move by the stations' own market settings:
    /// - NPC stations buy what their market buys and sell what it sells, for credits.
    /// - Player stations request goods set to Buying (up to the target stock) and provide goods
    ///   set to Selling (above the target stock). Goods set to Buying and Selling do both,
    ///   around the target stock. Goods move between player stations without payment.
    /// Cargo goes to the player's own requesting stations first, then to the best-paying NPC
    /// stops, each up to what it requests.
    /// </summary>
    internal static class Trader
    {
        private const int Goods = 3;
        private const int Unlimited = 999999;

        private struct Demand
        {
            public Station station;
            public float price;
            public int limit;
        }

        public static void TradeAt(PlayerFleetMember member, RouteShip ship, TradeRoute route, Station here)
        {
            List<Station> stops = UsableStops(route);
            Unload(member, here, stops);
            Load(member, ship, route, here, stops);
        }

        private static List<Station> UsableStops(TradeRoute route)
        {
            var stops = new List<Station>();
            foreach (int id in route.stations)
            {
                Station station = RouteStations.Get(id);
                if (RouteStations.IsUsable(station) && !stops.Contains(station))
                {
                    stops.Add(station);
                }
            }
            return stops;
        }

        private static void Unload(PlayerFleetMember member, Station here, List<Station> stops)
        {
            List<CargoItem> cargo = member.shipData.cargo;
            for (int i = cargo.Count - 1; i >= 0; i--)
            {
                CargoItem item = cargo[i];
                Item goods = item.itemType == Goods && item.stockStationID == -1 ? ItemDB.GetItem(item.itemID) : null;
                if (goods == null)
                {
                    continue;
                }
                List<Demand> demands = Demands(goods, stops, null);
                int deliver;
                if (demands.Count == 0)
                {
                    // No stop wants it any more: leave it at the first station that takes it.
                    deliver = AcceptsDump(here, goods) ? item.qnt : 0;
                }
                else
                {
                    deliver = ShareFor(here, demands, item.qnt);
                }
                if (deliver > 0)
                {
                    Deliver(here, goods, deliver);
                    item.qnt -= deliver;
                    if (item.qnt <= 0)
                    {
                        cargo.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>How much of <paramref name="qnt"/> goes to this stop when the best-paying stops fill first.</summary>
        private static int ShareFor(Station here, List<Demand> demands, int qnt)
        {
            // Best price first; on a tie, this stop first, since the ship is already here.
            demands.Sort((a, b) =>
            {
                int byPrice = b.price.CompareTo(a.price);
                if (byPrice != 0 || a.station == b.station)
                {
                    return byPrice;
                }
                return a.station == here ? -1 : b.station == here ? 1 : 0;
            });
            int left = qnt;
            foreach (Demand demand in demands)
            {
                int share = Mathf.Min(left, demand.limit);
                if (demand.station == here)
                {
                    return share;
                }
                left -= share;
                if (left <= 0)
                {
                    break;
                }
            }
            return 0;
        }

        private static void Load(PlayerFleetMember member, RouteShip ship, TradeRoute route, Station here, List<Station> stops)
        {
            if (!stops.Contains(here))
            {
                return;
            }
            float free = ShipEstimates.CargoSpace(member) - ShipEstimates.UsedCargoSpace(member);
            float budget = Mathf.Min(Plugin.MaxSpendPerStop.Value, PlayerCredits() - Plugin.KeepCredits.Value);

            var offers = new List<KeyValuePair<float, MarketItem>>();
            foreach (MarketItem mi in here.MarketModule.MarketList)
            {
                Item goods = mi.itemType == Goods ? mi.asItem : null;
                if (goods == null || goods.weight <= 0f || Available(here, mi) <= 0)
                {
                    continue;
                }
                float cost = here.PlayerOwned ? 0f : mi.GetItemPriceModified(here.Sector);
                float best = -1f;
                foreach (Demand demand in Demands(goods, stops, here))
                {
                    best = Mathf.Max(best, demand.price);
                }
                if (best > cost * (1f + Plugin.MinMargin.Value))
                {
                    offers.Add(new KeyValuePair<float, MarketItem>((best - cost) / goods.weight, mi));
                }
            }
            offers.Sort((a, b) => b.Key.CompareTo(a.Key));

            foreach (KeyValuePair<float, MarketItem> offer in offers)
            {
                MarketItem mi = offer.Value;
                Item goods = mi.asItem;
                float price = here.PlayerOwned ? 0f : mi.GetItemPriceModified(here.Sector);
                int wanted = 0;
                foreach (Demand demand in Demands(goods, stops, here))
                {
                    if (demand.price > price * (1f + Plugin.MinMargin.Value))
                    {
                        wanted = AddCapped(wanted, demand.limit);
                    }
                }
                wanted -= CarriedOnRoute(route, goods);
                int qnt = Mathf.Min(wanted, Available(here, mi), Mathf.FloorToInt(free / goods.weight));
                if (price > 0f)
                {
                    qnt = Mathf.Min(qnt, Mathf.FloorToInt(budget / price));
                }
                if (qnt <= 0)
                {
                    continue;
                }
                if (price > 0f)
                {
                    float total = qnt * price;
                    if (!PlayerControl.inst.GetCargoSystem.PayCreditCost(total, showErrorMsg: false))
                    {
                        continue;
                    }
                    here.AddCredits(total);
                    budget -= total;
                    TradeLog.Add($"{Ship(member)} bought {qnt} {goods.itemName} at {here.stationName(false)} for {total:N0}");
                }
                else
                {
                    TradeLog.Add($"{Ship(member)} loaded {qnt} {goods.itemName} at {here.stationName(false)}");
                }
                mi.AlterItemStock(-qnt);
                AddCargo(member, goods, qnt, price);
                free -= qnt * goods.weight;
            }
        }

        /// <summary>
        /// Stops other than <paramref name="except"/> that request the goods, with their price
        /// and how many they take.
        /// </summary>
        private static List<Demand> Demands(Item goods, List<Station> stops, Station except)
        {
            var demands = new List<Demand>();
            foreach (Station station in stops)
            {
                if (station == except)
                {
                    continue;
                }
                MarketItem mi = station.MarketModule.GetMarketItem(Goods, goods.id, goods.rarity, null);
                int limit = mi == null ? 0 : RequestLimit(station, mi);
                if (limit > 0)
                {
                    demands.Add(new Demand { station = station, price = DemandPrice(station, mi, limit), limit = limit });
                }
            }
            return demands;
        }

        /// <summary>
        /// The value used to rank a request. NPC stations rank by what they pay. The player's own
        /// requests come before any sale, the neediest first, since no money changes hands.
        /// </summary>
        private static float DemandPrice(Station station, MarketItem mi, int limit)
        {
            if (station.PlayerOwned)
            {
                return PlayerRequestRank + Mathf.Min(limit, Unlimited);
            }
            return mi.GetItemPriceModified(station.Sector);
        }

        private const float PlayerRequestRank = 1e9f;

        private static int RequestLimit(Station station, MarketItem mi)
        {
            if (!mi.IsBuying)
            {
                return 0;
            }
            if (!station.PlayerOwned)
            {
                return Mathf.Max(0, mi.BuyingLimit);
            }
            if (mi.TargetStock > 0)
            {
                return Mathf.Max(0, mi.TargetStock - mi.Stock);
            }
            // Buying with no target: take everything. Buying and Selling with no target would
            // make two such stations trade the same goods back and forth, so it requests nothing.
            return mi.JustBuying ? Unlimited : 0;
        }

        /// <summary>How many the station can give a route ship.</summary>
        private static int Available(Station station, MarketItem mi)
        {
            if (!mi.IsSelling)
            {
                return 0;
            }
            if (!station.PlayerOwned)
            {
                return Mathf.Max(0, mi.StockForSale);
            }
            // The station's own stock only: the player's stash counts at every player station,
            // and route ships shouldn't carry it off.
            int own = station.GetItemStock().GetStockCount(mi.itemType, mi.itemID, mi.rarity, mi.extraData);
            return Mathf.Max(0, own - mi.TargetStock);
        }

        private static bool AcceptsDump(Station station, Item goods)
        {
            if (station.PlayerOwned)
            {
                return true;
            }
            MarketItem mi = station.MarketModule.GetMarketItem(Goods, goods.id, goods.rarity, null);
            return mi != null && mi.IsBuying;
        }

        private static void Deliver(Station station, Item goods, int qnt)
        {
            MarketItem mi = station.MarketModule.GetMarketItem(Goods, goods.id, goods.rarity, null);
            if (station.PlayerOwned)
            {
                station.GetItemStock().AlterStock(goods, qnt);
                TradeLog.Add($"Delivered {qnt} {goods.itemName} to {station.stationName(false)}");
                return;
            }
            float total = qnt * mi.GetItemPriceModified(station.Sector);
            station.PayCredits(total);
            PlayerControl.inst.GetCargoSystem.AddCredits(total);
            mi.AlterItemStock(qnt);
            TradeLog.Add($"Sold {qnt} {goods.itemName} at {station.stationName(false)} for {total:N0}");
        }

        /// <summary>Goods already aboard ships on this route, which count against what the stops want.</summary>
        private static int CarriedOnRoute(TradeRoute route, Item goods)
        {
            int carried = 0;
            foreach (RouteShip other in RouteStore.Ships)
            {
                if (other.routeId != route.id)
                {
                    continue;
                }
                List<CargoItem> cargo = RouteStore.FindMember(other)?.shipData?.cargo;
                if (cargo == null)
                {
                    continue;
                }
                foreach (CargoItem item in cargo)
                {
                    if (item.itemType == Goods && item.itemID == goods.id && item.rarity == goods.rarity && item.stockStationID == -1)
                    {
                        carried += item.qnt;
                    }
                }
            }
            return carried;
        }

        private static void AddCargo(PlayerFleetMember member, Item goods, int qnt, float price)
        {
            List<CargoItem> cargo = member.shipData.cargo;
            CargoItem item = cargo.Find(c => c.itemType == Goods && c.itemID == goods.id && c.rarity == goods.rarity && c.stockStationID == -1 && c.extraData == null);
            if (item != null)
            {
                item.pricePaid = (item.pricePaid * item.qnt + price * qnt) / (item.qnt + qnt);
                item.qnt += qnt;
                return;
            }
            cargo.Add(new CargoItem { itemType = Goods, itemID = goods.id, rarity = goods.rarity, qnt = qnt, pricePaid = price });
        }

        private static int AddCapped(int a, int b)
        {
            return a >= Unlimited - b ? Unlimited : a + b;
        }

        private static float PlayerCredits()
        {
            return PlayerControl.inst.GetCargoSystem.credits;
        }

        private static string Ship(PlayerFleetMember member)
        {
            return member.CommanderName(14, colored: false);
        }
    }
}
