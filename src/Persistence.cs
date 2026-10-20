using System;
using System.IO;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

namespace StationTransport
{
    /// <summary>
    /// Keeps the mod's data in a sidecar file next to each vanilla save, so nothing new goes into
    /// the save itself and the mod can be removed. The file is named *.dat in Save_Data because
    /// Steam Cloud syncs Save_Data/*.dat and nothing else there.
    /// </summary>
    [HarmonyPatch]
    internal static class Persistence
    {
        private const string FilePrefix = "TradeRoutes_";
        private const string LastBackupName = "LastTradeRoutesBackup.dat";
        private const string TempName = "TempTradeRoutes.dat";

        /// <summary>Max difference between the sidecar's stamp and the save's play time.</summary>
        private const float StampTolerance = 0.5f;

        private static string SaveFolder => Path.GetDirectoryName(GameData.fullSaveGamePath);

        /// <summary>The sidecar for the current slot: "", "_Auto" or "_Bkp".</summary>
        private static string SidecarPath(string suffix)
        {
            return Path.Combine(SaveFolder, FilePrefix + GameData.gameFileIndex.ToString("00") + suffix + ".dat");
        }

        /// <summary>
        /// Runs before the game writes a save. GameData.SaveGame calls GetGameData on the main
        /// thread, then serialises on a worker thread; this prefix runs before either.
        /// </summary>
        [HarmonyPatch(typeof(GameData), nameof(GameData.SaveGame))]
        [HarmonyPrefix]
        private static void SaveGamePrefix(out string __state)
        {
            __state = null;
            if (!RouteSim.GameActive)
            {
                return;
            }
            RouteSim.BeginSave();
            try
            {
                RouteStore.Data.stamp = GameData.data.totalTimePlayed;
                __state = RouteJson.Write(RouteStore.Data);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Could not snapshot trade routes for saving: {e}");
            }
        }

        /// <summary>
        /// Swaps the save task for one that writes the sidecar once the vanilla save succeeds, then
        /// puts ships in flight back. The game's menu polls the task, so it waits for this too.
        /// </summary>
        [HarmonyPatch(typeof(GameData), nameof(GameData.SaveGame))]
        [HarmonyPostfix]
        private static void SaveGamePostfix(ref Task<bool> __result, string __state)
        {
            if (RouteSim.Saving)
            {
                __result = FinishSave(__result, __state, SidecarPath(GameData.saveType));
            }
        }

        private static async Task<bool> FinishSave(Task<bool> save, string json, string path)
        {
            try
            {
                bool saved = await save;
                if (saved && json != null)
                {
                    Write(path, json);
                }
                return saved;
            }
            finally
            {
                // Unity resumes awaits on the main thread, so this is safe to do here.
                RouteSim.EndSave();
            }
        }

        /// <summary>Temp file, then replace, keeping the previous version, like the vanilla save.</summary>
        private static void Write(string path, string json)
        {
            try
            {
                string temp = Path.Combine(SaveFolder, TempName);
                File.WriteAllText(temp, json);
                if (File.Exists(path))
                {
                    File.Replace(temp, path, Path.Combine(SaveFolder, LastBackupName));
                }
                else
                {
                    File.Move(temp, path);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Could not write {path}: {e}");
            }
        }

        [HarmonyPatch(typeof(GameData), "SetGameData")]
        [HarmonyPostfix]
        private static void SetGameDataPostfix(bool __result)
        {
            RouteStore.Clear();
            TradeLog.Clear();
            if (!__result || !Plugin.AllowedIn(GameData.data))
            {
                return;
            }
            string path = SidecarPath(GameData.saveType);
            if (!File.Exists(path))
            {
                return;
            }
            try
            {
                Load(RouteJson.Read(File.ReadAllText(path)), path);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Could not read {path}; trade routes not loaded: {e}");
                RouteStore.Clear();
            }
        }

        private static void Load(RouteSaveData data, string path)
        {
            RouteStore.Set(data);
            if (Mathf.Abs(data.stamp - GameData.data.totalTimePlayed) > StampTolerance)
            {
                // From another save point (a Steam Cloud mix-up, a copied save, a failed save).
                // Keep the routes, but trust the vanilla save for where each ship is.
                Plugin.Log.LogWarning($"{path} does not match this save; route ships restart from where the save has them docked.");
                foreach (RouteShip ship in RouteStore.Ships)
                {
                    PlayerFleetMember member = RouteStore.FindMember(ship);
                    ship.state = ShipState.Docked;
                    ship.stationId = member != null ? member.dockedStationID : -1;
                    ship.targetStationId = -1;
                    ship.tradePending = false;
                    ship.unassignPending = false;
                    ship.eventTime = 0f;
                }
            }
            RouteSim.RestoreTransitRecords();
            Plugin.Log.LogInfo($"Loaded {RouteStore.Routes.Count} trade routes and {RouteStore.Ships.Count} route ships");
        }

        [HarmonyPatch(typeof(GameData), nameof(GameData.NewGame))]
        [HarmonyPostfix]
        private static void NewGamePostfix()
        {
            RouteStore.Clear();
            TradeLog.Clear();
        }

        /// <summary>
        /// The game copies the save to its _Bkp file on load, at most every 5 minutes. Copy the
        /// sidecar alongside it so a backup restored by hand has matching routes.
        /// </summary>
        [HarmonyPatch(typeof(GameData), "VerifySaveFileBackup")]
        [HarmonyPrefix]
        private static void BackupPrefix()
        {
            if (DateTime.Now < GameManager.lastBackupSaved.AddMinutes(5.0))
            {
                return;
            }
            try
            {
                string source = SidecarPath(GameData.saveType);
                string backup = SidecarPath("_Bkp");
                if (File.Exists(source))
                {
                    File.Copy(source, backup, overwrite: true);
                }
                else if (File.Exists(backup))
                {
                    File.Delete(backup);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not back up trade routes: {e.Message}");
            }
        }

        /// <summary>Removes sidecars whose vanilla save is gone, so Steam Cloud doesn't keep orphans.</summary>
        [HarmonyPatch(typeof(MenuControl), nameof(MenuControl.DeleteSaveGame))]
        [HarmonyPostfix]
        private static void DeleteSavePostfix()
        {
            DeleteOrphans();
        }

        [HarmonyPatch(typeof(MenuControl), nameof(MenuControl.DeleteAutoSaveGame))]
        [HarmonyPostfix]
        private static void DeleteAutoSavePostfix()
        {
            DeleteOrphans();
        }

        private static void DeleteOrphans()
        {
            string slot = GameData.gameFileIndex.ToString("00");
            foreach (string suffix in new[] { "", "_Auto", "_Bkp" })
            {
                string vanilla = Path.Combine(SaveFolder, "SaveGameData_" + slot + suffix + ".dat");
                string sidecar = SidecarPath(suffix);
                try
                {
                    if (!File.Exists(vanilla) && File.Exists(sidecar))
                    {
                        File.Delete(sidecar);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Could not delete {sidecar}: {e.Message}");
                }
            }
        }
    }
}
