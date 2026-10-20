using System.Collections.Generic;
using UnityEngine;

namespace StationTransport
{
    /// <summary>
    /// Speed and hold size for a fleet ship that isn't spawned. The game only computes these on
    /// a live <see cref="SpaceShip"/> (SpaceShip.CalculateShipStats), so this repeats the parts
    /// that matter: hull, engines and cargo bays. Crew and character bonuses are left out, so the
    /// numbers are on the low side.
    /// </summary>
    internal static class ShipEstimates
    {
        private const int SpeedEffect = 4;
        private const int CargoSpaceEffect = 45;
        private const int ArmorEffect = 2;
        private const int HullBonusEffect = 28;
        private const float MinSpeed = 4f;

        public static float Speed(PlayerFleetMember member)
        {
            ShipModelData model = member.ModelData();
            if (model == null)
            {
                return MinSpeed;
            }
            float speed = EquipmentDB.GetEffect(Equipments(member), SpeedEffect) * model.speed / 10f;
            ShipClassDefinition[] classes = GameDefinitions.shipClassDef;
            int classIndex = (int)model.shipClass - 1;
            if (classes != null && classIndex >= 0 && classIndex < classes.Length)
            {
                speed = Mathf.Min(speed, classes[classIndex].maxVelocity);
            }
            return Mathf.Max(speed, MinSpeed);
        }

        public static int CargoSpace(PlayerFleetMember member)
        {
            ShipModelData model = member.ModelData();
            if (model == null)
            {
                return 0;
            }
            return Mathf.RoundToInt(model.cargoSpace * (1f + EquipmentDB.GetEffect(Equipments(member), CargoSpaceEffect) / 100f));
        }

        /// <summary>
        /// Full hull points: the hull's base, multiplied by installed armour if any, plus flat hull
        /// bonuses, as SpaceShip.CalculateShipStats works it out.
        /// </summary>
        public static float HullPoints(PlayerFleetMember member)
        {
            ShipModelData model = member.ModelData();
            if (model == null)
            {
                return 0f;
            }
            List<InstalledEquipment> equipments = Equipments(member);
            float armor = EquipmentDB.GetEffect(equipments, ArmorEffect);
            float hull = armor > 0f ? armor * model.hullPoints : model.hullPoints;
            return hull + EquipmentDB.GetEffect(equipments, HullBonusEffect);
        }

        /// <summary>Hold space taken, as CargoSystem.GetUsedCargoSpace counts it.</summary>
        public static float UsedCargoSpace(PlayerFleetMember member)
        {
            float used = 0f;
            List<CargoItem> cargo = member.shipData?.cargo;
            if (cargo == null)
            {
                return used;
            }
            foreach (CargoItem item in cargo)
            {
                if (item.stockStationID != -1)
                {
                    continue;
                }
                switch (item.itemType)
                {
                    case 1:
                        used += GameData.data.weaponList[item.itemID].space * item.qnt;
                        break;
                    case 2:
                        used += EquipmentDB.GetEquipment(item.itemID).space * item.qnt;
                        break;
                    case 3:
                        used += ItemDB.GetItem(item.itemID).weight * item.qnt;
                        break;
                    case 4:
                        used += ShipDB.GetModel(item.itemID).spaceOcupied * item.qnt;
                        break;
                }
            }
            return used;
        }

        private static List<InstalledEquipment> Equipments(PlayerFleetMember member)
        {
            return member.shipData?.equipments ?? new List<InstalledEquipment>();
        }
    }
}
