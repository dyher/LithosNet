using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LithosNet.VM {
    public struct Location { 
        public string Env; 
        public int X; 
        public int Y; 
        public int Z; 
    }

    public static class SpaceManager {
        private static readonly ConcurrentDictionary<string, Location> _positions = new();

        public static void Move(string obj, int x, int y, int z) {
            _positions.AddOrUpdate(obj, 
                new Location { Env = "world", X = x, Y = y, Z = z }, 
                (k, v) => { v.X = x; v.Y = y; v.Z = z; return v; });
            Console.WriteLine($"🌍 [Space] {obj} 移動到 ({x}, {y}, {z})");
        }

        public static List<string> GetObjectsInRadius(int x, int y, int z, int radius) {
            var result = new List<string>();
            int r2 = radius * radius;
            foreach(var kvp in _positions) {
                var loc = kvp.Value;
                int dx = loc.X - x, dy = loc.Y - y, dz = loc.Z - z;
                if (dx*dx + dy*dy + dz*dz <= r2) {
                    result.Add(kvp.Key);
                }
            }
            return result;
        }
    }
}
