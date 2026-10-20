using System.Collections.Generic;
using UnityEngine;

namespace StationTransport
{
    /// <summary>
    /// Draws each trade route on the galaxy map, following the jump gates its ships use. Lines use
    /// the game's gate-line material, which is additive and bloomed by the map camera, so they're
    /// tinted well down and drawn as soft dashes that roll along each leg in stop order. The route
    /// being edited is a little wider and brighter.
    /// </summary>
    internal static class RouteLines
    {
        private const string RootName = "TradeRouteLines";
        private const string LinePrefab = "Effects/GateConnection";

        /// <summary>Raised toward the camera so the lines draw over the gate lines.</summary>
        private const float Lift = 0.1f;

        // The game draws gate lines 0.2 wide (0.4 for the player's sector).
        private const float Width = 0.15f;
        private const float HighlightWidth = 0.25f;

        /// <summary>How much of the palette colour reaches the additive shader.</summary>
        private const float Brightness = 0.22f;
        private const float HighlightBrightness = 0.4f;

        /// <summary>Dashes per map unit; neighbouring sectors are 1–3 units apart.</summary>
        private const float DashesPerUnit = 3f;

        /// <summary>Map units per second the dashes travel.</summary>
        private const float RollSpeed = 0.6f;

        private static readonly Color[] Palette =
        {
            new Color(1f, 0.75f, 0.2f),
            new Color(0.35f, 0.9f, 0.45f),
            new Color(1f, 0.4f, 0.75f),
            new Color(0.45f, 0.75f, 1f),
            new Color(1f, 0.5f, 0.3f),
            new Color(0.75f, 0.55f, 1f),
            new Color(0.3f, 0.95f, 0.9f),
            new Color(0.95f, 0.95f, 0.5f),
        };

        private static Texture2D dashTexture;

        public static Color ColorFor(TradeRoute route)
        {
            return Palette[Mathf.Abs(route.id - 1) % Palette.Length];
        }

        public static void Redraw(GalaxyMap map, TradeRoute highlighted)
        {
            if (map == null)
            {
                return;
            }
            Transform objects = map.transform.Find("Objects");
            Transform tiles = objects != null ? objects.Find("Temp") : null;
            if (tiles == null)
            {
                return;
            }
            Transform root = objects.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName).transform;
                root.SetParent(objects, false);
                root.gameObject.AddComponent<Roller>();
            }
            Roller roller = root.GetComponent<Roller>();
            roller.Materials.Clear();
            UiKit.Clear(root);
            if (!RouteSim.GameActive)
            {
                return;
            }
            GameObject prefab = ObjManager.GetObj(LinePrefab);
            if (prefab == null)
            {
                return;
            }
            foreach (TradeRoute route in RouteStore.Routes)
            {
                DrawRoute(route, route == highlighted, prefab, root, tiles, roller);
            }
        }

        private static void DrawRoute(TradeRoute route, bool highlighted, GameObject prefab, Transform root, Transform tiles, Roller roller)
        {
            List<int> stops = route.stations;
            int legs = route.circular && stops.Count > 2 ? stops.Count : stops.Count - 1;
            for (int i = 0; i < legs; i++)
            {
                Station from = GameData.GetStation(stops[i], allowDestroyedOrWrecked: true);
                Station to = GameData.GetStation(stops[(i + 1) % stops.Count], allowDestroyedOrWrecked: true);
                if (from == null || to == null || from.sectorIndex == to.sectorIndex)
                {
                    continue;
                }
                // No gate path: draw the leg straight, so the player can see it's broken.
                List<int> sectors = GateGraph.SectorPath(from.sectorIndex, to.sectorIndex)
                    ?? new List<int> { from.sectorIndex, to.sectorIndex };
                var points = new Vector3[sectors.Count];
                for (int s = 0; s < sectors.Count; s++)
                {
                    TSector sector = GameData.data.sectors[sectors[s]];
                    points[s] = tiles.TransformPoint(new Vector3(sector.realX, Lift, sector.realY));
                }
                GameObject line = Object.Instantiate(prefab, root, false);
                line.name = route.name;
                LineRenderer renderer = line.GetComponent<LineRenderer>();
                renderer.useWorldSpace = true;
                renderer.positionCount = points.Length;
                renderer.SetPositions(points);
                float width = highlighted ? HighlightWidth : Width;
                renderer.startWidth = width;
                renderer.endWidth = width;

                Material material = renderer.material;
                float k = highlighted ? HighlightBrightness : Brightness;
                Color color = ColorFor(route);
                material.SetColor("_TintColor", new Color(color.r * k, color.g * k, color.b * k, 0.5f));
                if (material.HasProperty("_MainTex"))
                {
                    renderer.textureMode = LineTextureMode.Tile;
                    material.mainTexture = DashTexture();
                    material.mainTextureScale = new Vector2(DashesPerUnit, 1f);
                    roller.Materials.Add(material);
                }
            }
        }

        /// <summary>
        /// One dash and gap along the line, soft across its width. Black where empty, since the
        /// shader is additive and black adds nothing.
        /// </summary>
        private static Texture2D DashTexture()
        {
            if (dashTexture != null)
            {
                return dashTexture;
            }
            const int length = 32;
            const int across = 16;
            const float dash = 0.55f;
            dashTexture = new Texture2D(length, across, TextureFormat.RGBA32, mipChain: false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            for (int x = 0; x < length; x++)
            {
                float u = (x + 0.5f) / length;
                // Soft ends on each dash.
                float along = Mathf.Clamp01(Mathf.Min(u, dash - u) * 12f);
                for (int y = 0; y < across; y++)
                {
                    float v = (y + 0.5f) / across * 2f - 1f;
                    float a = along * Mathf.Clamp01(1f - v * v);
                    dashTexture.SetPixel(x, y, new Color(a, a, a, a));
                }
            }
            dashTexture.Apply();
            return dashTexture;
        }

        /// <summary>Scrolls the dashes so they roll from each stop toward the next.</summary>
        private class Roller : MonoBehaviour
        {
            public readonly List<Material> Materials = new List<Material>();

            private void Update()
            {
                // The map may be open while the game is paused, so use unscaled time.
                // The texture repeats, so wrap the offset to keep it small.
                var offset = new Vector2(-Mathf.Repeat(Time.unscaledTime * RollSpeed * DashesPerUnit, 1f), 0f);
                foreach (Material material in Materials)
                {
                    if (material != null)
                    {
                        material.mainTextureOffset = offset;
                    }
                }
            }
        }
    }
}
