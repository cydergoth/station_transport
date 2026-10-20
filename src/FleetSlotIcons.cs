using UnityEngine;
using UnityEngine.UI;

namespace StationTransport
{
    /// <summary>
    /// The fleet panel's icons for route ships. The launch/dock button gets the game's circling
    /// arrows icon instead of Launch, and the role button a money bag instead of the combat role.
    /// Each is an extra child copied from a game icon, so size and placement match.
    /// </summary>
    internal static class FleetSlotIcons
    {
        private const string RouteIconName = "ImgTradeRoute";
        private const string TraderIconName = "ImgTrader";
        private const string RouteSprite = "Sprites/Freighter";
        private const string TraderSprite = "Sprites/gold_bag";

        /// <summary>The role button's first children are the three role icons, by index.</summary>
        private const int RoleIconCount = 3;

        public static void Apply(FleetMemberSlot slot, bool routeShip)
        {
            Transform launchDock = GameAccess.SlotLaunchDock(slot);
            if (launchDock != null)
            {
                Transform launch = launchDock.Find("ImgLaunch");
                Transform dock = launchDock.Find("ImgDock");
                GameObject icon = Icon(launchDock, launch, RouteIconName, RouteSprite, routeShip);
                if (routeShip && icon != null)
                {
                    launch?.gameObject.SetActive(false);
                    dock?.gameObject.SetActive(false);
                }
            }

            Transform role = GameAccess.SlotRole(slot);
            if (role != null && role.childCount >= RoleIconCount)
            {
                GameObject icon = Icon(role, role.GetChild(0), TraderIconName, TraderSprite, routeShip);
                if (routeShip && icon != null)
                {
                    for (int i = 0; i < RoleIconCount; i++)
                    {
                        role.GetChild(i).gameObject.SetActive(false);
                    }
                }
            }
        }

        /// <summary>
        /// Shows or hides the extra icon, creating it on first use from <paramref name="template"/>.
        /// Null if it can't be made, in which case the game's icons are left alone.
        /// </summary>
        private static GameObject Icon(Transform parent, Transform template, string name, string spritePath, bool show)
        {
            Transform existing = parent.Find(name);
            if (existing == null)
            {
                if (!show || template == null)
                {
                    return null;
                }
                Sprite sprite = ObjManager.GetSprite(spritePath);
                if (sprite == null)
                {
                    return null;
                }
                existing = Object.Instantiate(template.gameObject, parent, false).transform;
                existing.name = name;
                existing.SetAsLastSibling();
                existing.GetComponent<Image>().sprite = sprite;
            }
            existing.gameObject.SetActive(show);
            return existing.gameObject;
        }
    }
}
