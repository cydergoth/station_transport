using HarmonyLib;

namespace StationTransport
{
    /// <summary>Redraws the mod's UI when the player changes the game language.</summary>
    [HarmonyPatch]
    internal static class LangPatches
    {
        [HarmonyPatch(typeof(Lang), nameof(Lang.SetLanguageTo))]
        [HarmonyPostfix]
        private static void SetLanguageToPostfix()
        {
            // Not '?.': Unity objects override == null, which the null-conditional operator ignores.
            MapPanel panel = MapPanel.Instance;
            if (panel != null)
            {
                panel.RefreshLanguage();
            }
            // The hangar button is relabelled each time an item is selected.
            HangarUI.Hide();
        }
    }
}
