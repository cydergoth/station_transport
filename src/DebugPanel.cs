using UnityEngine;

namespace StationTransport
{
    /// <summary>
    /// A developer window with recent route activity and the simulation's timings. Routes are made
    /// on the galaxy map and ships assigned in the Hangar; this only reports. English only.
    /// </summary>
    internal class DebugPanel
    {
        private const int WindowId = 0x57A7;

        private bool visible;
        private Rect window = new Rect(40, 40, 620, 520);
        private Vector2 scroll;

        public void Update()
        {
            if (Plugin.TogglePanel.Value.IsDown())
            {
                visible = !visible;
            }
        }

        public void OnGUI()
        {
            if (visible)
            {
                window = GUILayout.Window(WindowId, window, Draw, "Station Transport log");
            }
        }

        private void Draw(int id)
        {
            GUILayout.Label($"Routes: {RouteStore.Routes.Count}   Route ships: {RouteStore.Ships.Count}");
            GUILayout.Label($"Tick: {TradeLog.Tick}");
            GUILayout.Label($"Stop: {TradeLog.Stop}");
            GUILayout.Label($"Stops delayed by the station lock: {TradeLog.LockMisses}");
            GUILayout.Space(6);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(window.height - 150));
            for (int i = TradeLog.Lines.Count - 1; i >= 0; i--)
            {
                GUILayout.Label(TradeLog.Lines[i]);
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button("Close"))
            {
                visible = false;
            }
            GUI.DragWindow();
        }
    }
}
