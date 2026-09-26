using LithosNet.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace LithosNet.VM {
    public struct Location { 
        public string Env; 
        public int X; 
        public int Y; 
        public int Z; 
    }

    public static class SpaceManager {
        private static readonly ConcurrentDictionary<string, Location> _positions = new();

        public static void Move(string obj, int x, int y, int z, ObjectManager objMgr, int radius = 5) {
            bool hasOld = _positions.TryGetValue(obj, out Location oldLoc);
            Location newLoc = new Location { Env = "world", X = x, Y = y, Z = z };
            _positions[obj] = newLoc;
            

            if (objMgr == null) return;

            var oldSet = hasOld ? GetObjectsInRadius(oldLoc.X, oldLoc.Y, oldLoc.Z, radius) : new List<string>();
            var newSet = GetObjectsInRadius(x, y, z, radius);

            // 找出「新進入視野」的物件 (在 newSet 但不在 oldSet)
            var entered = newSet.Except(oldSet).Where(o => o != obj).ToList();
            // 找出「離開視野」的物件 (在 oldSet 但不在 newSet)
            var left = oldSet.Except(newSet).Where(o => o != obj).ToList();

            foreach (var target in entered) {
                try { 
                    objMgr.CallFunction(target, "aoi_enter", LpcValue.Create(obj)); 
                } catch {}
            }

            foreach (var target in left) {
                try { 
                    objMgr.CallFunction(target, "aoi_leave", LpcValue.Create(obj)); 
                } catch {}
            }
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
