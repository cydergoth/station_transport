using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace StationTransport
{
    /// <summary>
    /// The mod's own UI strings, translated with the game's language setting.
    ///
    /// Files use the game's <c>Language/&lt;language&gt;_main.txt</c> format: entries are
    /// "<c>NNN text|</c>", lines starting with <c>#</c> are comments, and <c>&lt;par1&gt;</c>,
    /// <c>&lt;par2&gt;</c>... are replaced with values. They live in the plugin's
    /// <c>Language</c> folder and are named after <see cref="Lang.GetLanguageName"/>
    /// (english, german, chinese...). Unlike the game, entries are looked up by their number,
    /// not their position, so a translation may leave entries out; missing entries fall back to
    /// the English text built into the DLL.
    /// </summary>
    internal static class Loc
    {
        // Hangar
        public const int TransferToRoute = 0;
        public const int TransferTitle = 1;
        public const int RemoveFromRoute = 2;
        public const int LeavingRoute = 3;
        public const int RouteShipButton = 4;
        public const int ChooseRoute = 5;
        public const int Cancel = 6;
        public const int TradeRoutes = 7;
        public const int MoreRoutes = 8;
        public const int NoRoutesMakeOnMap = 9;
        public const int NeedsHangar = 10;

        // Fleet panel
        public const int OnTradeRoute = 11;
        public const int InRouteReserve = 12;
        public const int TraderRole = 13;

        // Messages
        public const int EmergencyWarped = 14;
        public const int ShipLost = 15;
        public const int YourShip = 16;
        public const int RepairShort = 17;
        public const int StuckNoGate = 18;
        public const int StuckTooFewStops = 19;

        // Ship status
        public const int StatusNoGate = 20;
        public const int StatusTooFewStops = 21;
        public const int StatusReserve = 22;
        public const int StatusTo = 23;
        public const int StatusWarpedTo = 24;
        public const int StatusHull = 25;
        public const int StatusTrading = 26;
        public const int StatusLeaving = 27;

        // Routes and stations
        public const int RouteName = 28;
        public const int StationGone = 29;

        // Galaxy map panel
        public const int NoRoutesYet = 30;
        public const int LoopExplained = 31;
        public const int ThereAndBackExplained = 32;
        public const int DeleteRoute = 33;
        public const int Stops = 34;
        public const int NeedsTwoStops = 35;
        public const int NotUsable = 36;
        public const int Up = 37;
        public const int Remove = 38;
        public const int Split = 39;
        public const int BackTo = 40;
        public const int OpenLoop = 41;
        public const int CloseLoopBackTo = 42;
        public const int RemoveHelpLoop = 43;
        public const int RemoveHelp = 44;
        public const int Ships = 45;
        public const int NoShips = 46;
        public const int Stations = 47;
        public const int SelectSector = 48;
        public const int StationsIn = 49;
        public const int NoTradingStations = 50;
        public const int OwnerYou = 51;
        public const int OwnerNpc = 52;
        public const int CloseLoop = 53;
        public const int StopNumber = 54;
        public const int Add = 55;
        public const int New = 56;
        public const int NoGateRouteFrom = 57;
        public const int OnTradeRouteWarning = 58;
        public const int Rename = 59;

        private const string EnglishResource = "StationTransport.Language.english_main.txt";

        private static Dictionary<int, string> english;
        private static Dictionary<int, string> current;
        private static int loadedLanguage = -1;

        public static string Get(int code)
        {
            Validate();
            if (current.TryGetValue(code, out string text) || english.TryGetValue(code, out text))
            {
                return text;
            }
            return "LANG. ERROR on [" + Plugin.Name + "] " + code;
        }

        public static string Get(int code, params object[] pars)
        {
            string text = Get(code);
            for (int i = 0; i < pars.Length; i++)
            {
                text = text.Replace("<par" + (i + 1) + ">", pars[i]?.ToString() ?? "");
            }
            return text;
        }

        /// <summary>Reloads the translation if the game language changed since the last load.</summary>
        public static void Validate()
        {
            int language = Lang.current;
            if (language == loadedLanguage && current != null)
            {
                return;
            }
            loadedLanguage = language;
            if (english == null)
            {
                english = LoadEnglish();
            }
            string name = Lang.GetLanguageName(language);
            current = name == "english" ? english : LoadFile(name) ?? english;
        }

        private static Dictionary<int, string> LoadEnglish()
        {
            // The English text is embedded so the mod still works if the Language folder is missing.
            // Entries in a file on disk override it, which lets people fix wording without rebuilding.
            Dictionary<int, string> entries;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EnglishResource))
            using (var reader = new StreamReader(stream))
            {
                entries = Parse(reader.ReadToEnd());
            }
            Dictionary<int, string> onDisk = LoadFile("english");
            if (onDisk != null)
            {
                foreach (KeyValuePair<int, string> entry in onDisk)
                {
                    entries[entry.Key] = entry.Value;
                }
            }
            return entries;
        }

        private static Dictionary<int, string> LoadFile(string languageName)
        {
            string dir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Language");
            if (!Directory.Exists(dir))
            {
                return null;
            }
            // The game ships "Japanese_main.txt" but asks for "japanese", so match case-insensitively
            // (Linux file systems are case-sensitive).
            string wanted = languageName + "_main.txt";
            string path = Directory.GetFiles(dir, "*_main.txt")
                .FirstOrDefault(f => string.Equals(Path.GetFileName(f), wanted, StringComparison.OrdinalIgnoreCase));
            if (path == null)
            {
                return null;
            }
            try
            {
                return Parse(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not read translation {path}: {e.Message}");
                return null;
            }
        }

        private static Dictionary<int, string> Parse(string contents)
        {
            var entries = new Dictionary<int, string>();
            foreach (string raw in contents.Split('|'))
            {
                string entry = raw.TrimStart('﻿', '\r', '\n');
                if (entry.Length == 0 || entry[0] == '#' || entry[0] == '[')
                {
                    continue;
                }
                int digits = 0;
                while (digits < entry.Length && char.IsDigit(entry[digits]))
                {
                    digits++;
                }
                if (digits == 0 || !int.TryParse(entry.Substring(0, digits), out int code))
                {
                    continue;
                }
                // "NNN text": skip the single separator space after the number.
                entries[code] = digits < entry.Length && entry[digits] == ' ' ? entry.Substring(digits + 1) : entry.Substring(digits);
            }
            return entries;
        }
    }
}
