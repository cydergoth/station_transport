using System;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace StationTransport
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "starvalor.stationtransport";
        public const string Name = "Station Transport";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<float> DockSeconds;
        internal static ConfigEntry<float> GateTransitSeconds;
        internal static ConfigEntry<float> TravelTimeScale;
        internal static ConfigEntry<float> MaxSpendPerStop;
        internal static ConfigEntry<float> KeepCredits;
        internal static ConfigEntry<float> MinMargin;
        internal static ConfigEntry<bool> ExemptFromFleetCommander;
        internal static ConfigEntry<bool> OffscreenRisk;
        internal static ConfigEntry<float> RiskChance;
        internal static ConfigEntry<float> EmergencyWarpDelaySeconds;
        internal static ConfigEntry<KeyboardShortcut> TogglePanel;
        internal static ConfigEntry<bool> LogTrades;
        internal static ConfigEntry<bool> MapPanelOpen;

        private bool loaded;
        private readonly DebugPanel panel = new DebugPanel();

        private void Awake()
        {
            Log = Logger;
            DockSeconds = Config.Bind("Routes", "DockSeconds", 20f,
                "How long a route ship stays docked at each stop to load and unload, in seconds of play time.");
            GateTransitSeconds = Config.Bind("Routes", "GateTransitSeconds", 15f,
                "Extra travel time for each jump gate a route ship passes through, in seconds.");
            TravelTimeScale = Config.Bind("Routes", "TravelTimeScale", 1f,
                "Multiplies the time a route ship takes to fly across a sector. 1 is the ship's estimated top speed.");
            MaxSpendPerStop = Config.Bind("Trading", "MaxSpendPerStop", 20000f,
                "The most credits a route ship spends buying goods at one NPC station.");
            KeepCredits = Config.Bind("Trading", "KeepCredits", 10000f,
                "Route ships don't buy goods if it would leave you with fewer credits than this.");
            MinMargin = Config.Bind("Trading", "MinMargin", 0.05f,
                "Route ships only buy goods from NPC stations to sell on when the sale price beats the purchase price by this fraction (0.05 = 5%).");
            ExemptFromFleetCommander = Config.Bind("Fleet", "ExemptFromFleetCommander", true,
                "Ships on a trade route don't count against your Fleet Commander limit.");
            // Insert: the game binds every F-key (F5/F9 quick save/load), and desktops such as KDE
            // take Ctrl+F-keys.
            OffscreenRisk = Config.Bind("Danger", "OffscreenRisk", true,
                "Route ships flying through a sector where a faction hostile to you is fighting can be attacked.");
            RiskChance = Config.Bind("Danger", "RiskChance", 0.15f,
                "The chance a route ship is hit in each such sector it passes through (0.15 = 15%).");
            EmergencyWarpDelaySeconds = Config.Bind("Danger", "EmergencyWarpDelaySeconds", 60f,
                "How much later a route ship that emergency-warped out of a fight arrives at its stop.");
            TogglePanel = Config.Bind("Debug", "TogglePanel", new KeyboardShortcut(KeyCode.Insert),
                "Opens a log of recent route activity and timings, for troubleshooting.");
            LogTrades = Config.Bind("Debug", "LogTrades", false,
                "Writes every route arrival, departure and trade to the BepInEx log.");
            MapPanelOpen = Config.Bind("Map", "PanelOpen", true,
                "Whether the trade route panel on the galaxy map is open. The map's Trade routes button switches it.");

            var harmony = new Harmony(Guid);
            try
            {
                // Bind the game's non-public members now, so a game update that renames one
                // disables the mod at startup instead of failing mid-game.
                RuntimeHelpers.RunClassConstructor(typeof(GameAccess).TypeHandle);
                harmony.PatchAll(typeof(FleetPatches));
                harmony.PatchAll(typeof(Persistence));
                harmony.PatchAll(typeof(HangarPatches));
                harmony.PatchAll(typeof(MapPatches));
                harmony.PatchAll(typeof(LangPatches));
            }
            catch (Exception e)
            {
                harmony.UnpatchSelf();
                Log.LogError($"{Name} is not compatible with this version of the game and is disabled: {e}");
                return;
            }
            loaded = true;
            Log.LogInfo($"{Name} {Version} loaded");
        }

        /// <summary>Trade routes need the Base Building DLC and don't run in Arena mode.</summary>
        internal static bool AllowedIn(GameDataInfo data)
        {
            return data != null && !data.IsArena && GameData.HasExpansion(Expansion.BaseBuilding);
        }

        private void Update()
        {
            if (!loaded)
            {
                return;
            }
            RouteSim.Tick();
            panel.Update();
        }

        private void OnGUI()
        {
            if (loaded)
            {
                panel.OnGUI();
            }
        }
    }
}
