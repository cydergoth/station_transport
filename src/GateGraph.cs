using System.Collections.Generic;
using UnityEngine;

namespace StationTransport
{
    /// <summary>
    /// Routes between sectors through jump gates: the natural gates the player has discovered,
    /// plus player-built jump gate stations. Ships fly by gate only, never by warp drive.
    /// </summary>
    internal static class GateGraph
    {
        /// <summary>One gate crossing: leave sector A at one point, arrive in sector B at another.</summary>
        private struct Edge
        {
            public int toSector;
            public Vector2 exit;
            public Vector2 entry;
        }

        /// <summary>
        /// Travel time in seconds between two stations, or -1 if no chain of gates connects them.
        /// Uses the path with the fewest gate jumps.
        /// </summary>
        public static float TravelTime(Station from, Station to, float speed)
        {
            if (from == null || to == null || speed <= 0f)
            {
                return -1f;
            }
            List<Edge> path = FindPath(from.sectorIndex, to.sectorIndex);
            if (path == null)
            {
                return -1f;
            }
            float distance = 0f;
            Vector2 position = new Vector2(from.x, from.y);
            foreach (Edge edge in path)
            {
                distance += Vector2.Distance(position, edge.exit);
                position = edge.entry;
            }
            distance += Vector2.Distance(position, new Vector2(to.x, to.y));
            return distance / speed * Plugin.TravelTimeScale.Value + path.Count * Plugin.GateTransitSeconds.Value;
        }

        /// <summary>
        /// The sectors a ship passes through between two sectors, both ends included, or null if
        /// no chain of gates connects them.
        /// </summary>
        public static List<int> SectorPath(int fromSector, int toSector)
        {
            List<Edge> path = FindPath(fromSector, toSector);
            if (path == null)
            {
                return null;
            }
            var sectors = new List<int> { fromSector };
            foreach (Edge edge in path)
            {
                sectors.Add(edge.toSector);
            }
            return sectors;
        }

        /// <summary>Breadth-first search over gate links. Empty list for the same sector.</summary>
        private static List<Edge> FindPath(int fromSector, int toSector)
        {
            if (fromSector == toSector)
            {
                return new List<Edge>();
            }
            Dictionary<int, List<Edge>> graph = BuildGraph();
            var cameFrom = new Dictionary<int, KeyValuePair<int, Edge>>();
            var queue = new Queue<int>();
            queue.Enqueue(fromSector);
            cameFrom[fromSector] = default;
            while (queue.Count > 0)
            {
                int sector = queue.Dequeue();
                if (sector == toSector)
                {
                    var path = new List<Edge>();
                    while (sector != fromSector)
                    {
                        KeyValuePair<int, Edge> step = cameFrom[sector];
                        path.Add(step.Value);
                        sector = step.Key;
                    }
                    path.Reverse();
                    return path;
                }
                if (!graph.TryGetValue(sector, out List<Edge> edges))
                {
                    continue;
                }
                foreach (Edge edge in edges)
                {
                    if (!cameFrom.ContainsKey(edge.toSector))
                    {
                        cameFrom[edge.toSector] = new KeyValuePair<int, Edge>(sector, edge);
                        queue.Enqueue(edge.toSector);
                    }
                }
            }
            return null;
        }

        private static Dictionary<int, List<Edge>> BuildGraph()
        {
            var graph = new Dictionary<int, List<Edge>>();
            List<TSector> sectors = GameData.data.sectors;
            for (int i = 0; i < sectors.Count; i++)
            {
                List<JumpGate> gates = sectors[i].jumpGates;
                if (gates == null)
                {
                    continue;
                }
                foreach (JumpGate gate in gates)
                {
                    int to = gate.connectedSector;
                    if (!gate.discovered || to < 0 || to >= sectors.Count)
                    {
                        continue;
                    }
                    Add(graph, i, new Edge
                    {
                        toSector = to,
                        exit = new Vector2(gate.x, gate.y),
                        entry = ArrivalPoint(sectors[to], i),
                    });
                }
            }
            foreach (Station station in GameData.data.stationList)
            {
                if (station is JumpgateStation gate && gate.InActivity && !gate.DestroyedOrWrecked
                    && gate.connectedJumpgateStation is JumpgateStation other && other.InActivity && !other.DestroyedOrWrecked)
                {
                    Add(graph, gate.sectorIndex, new Edge
                    {
                        toSector = other.sectorIndex,
                        exit = new Vector2(gate.x, gate.y),
                        entry = new Vector2(other.x, other.y),
                    });
                }
            }
            return graph;
        }

        /// <summary>Where a ship comes out in a sector: the gate back to where it came from.</summary>
        private static Vector2 ArrivalPoint(TSector sector, int fromSector)
        {
            if (sector.jumpGates != null)
            {
                foreach (JumpGate gate in sector.jumpGates)
                {
                    if (gate.connectedSector == fromSector)
                    {
                        return new Vector2(gate.x, gate.y);
                    }
                }
            }
            return Vector2.zero;
        }

        private static void Add(Dictionary<int, List<Edge>> graph, int sector, Edge edge)
        {
            if (!graph.TryGetValue(sector, out List<Edge> edges))
            {
                edges = new List<Edge>();
                graph[sector] = edges;
            }
            edges.Add(edge);
        }
    }
}
