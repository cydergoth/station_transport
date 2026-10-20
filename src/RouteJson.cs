using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace StationTransport
{
    /// <summary>
    /// Reads and writes <see cref="RouteSaveData"/> as JSON. Not Unity's JsonUtility: its native
    /// serializer only knows classes from the game's own assemblies, so it silently drops lists of
    /// a plugin's classes (the routes and ships). This one works by reflection.
    /// </summary>
    internal static class RouteJson
    {
        private static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(typeof(RouteSaveData));

        public static string Write(RouteSaveData data)
        {
            using (var stream = new MemoryStream())
            {
                Serializer.WriteObject(stream, data);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        /// <summary>
        /// Fields missing from the file come back as their type's default, not the initializer
        /// value; <see cref="RouteStore.Set"/> fills in missing lists.
        /// </summary>
        public static RouteSaveData Read(string json)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (RouteSaveData)Serializer.ReadObject(stream);
            }
        }
    }
}
