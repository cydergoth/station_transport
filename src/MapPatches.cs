using System;
using HarmonyLib;

namespace StationTransport
{
    /// <summary>Adds the trade route panel and route lines to the galaxy map.</summary>
    [HarmonyPatch]
    internal static class MapPatches
    {
        [HarmonyPatch(typeof(GalaxyMap), "Awake")]
        [HarmonyPostfix]
        private static void AwakePostfix(GalaxyMap __instance)
        {
            try
            {
                MapPanel.Create(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Could not add the trade route panel to the galaxy map: {e}");
            }
        }

        [HarmonyPatch(typeof(GalaxyMap), nameof(GalaxyMap.ShowHideGalaxyMap))]
        [HarmonyPostfix]
        private static void ShowHidePostfix(GalaxyMap __instance)
        {
            if (!__instance.active)
            {
                return;
            }
            try
            {
                // Not '?.': Unity objects override == null, which the null-conditional operator ignores.
                MapPanel panel = MapPanel.Instance;
                if (panel != null)
                {
                    panel.OnMapOpened();
                }
                RouteLines.Redraw(__instance, panel != null ? panel.Editing : null);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Could not draw trade routes on the galaxy map: {e}");
            }
        }
    }
}
