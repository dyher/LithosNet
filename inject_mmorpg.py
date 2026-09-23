import os

# 1. 建立 GridMapManager.cs
grid_code = """#nullable disable
using System;
using System.Collections.Generic;

namespace LithosNet.VM {
    // 【MMORPG 核心】高效能空間索引網格 (Spatial Hash Grid)
    public static class GridMapManager {
        private const int CELL_SIZE = 5; // 每個網格大小為 5x5
        private static readonly Dictionary<string, (int x, int y)> _entityPositions = new();
        private static readonly Dictionary<string, HashSet<string>> _gridCells = new();

        private static string GetCellKey(int x, int y) => $"{x / CELL_SIZE}_{y / CELL_SIZE}";

        public static void Register(string objName, int x, int y) {
            _entityPositions[objName] = (x, y);
            string cellKey = GetCellKey(x, y);
            if (!_gridCells.ContainsKey(cellKey)) _gridCells[cellKey] = new HashSet<string>();
            _gridCells[cellKey].Add(objName);
        }

        public static void Move(string objName, int newX, int newY) {
            if (_entityPositions.TryGetValue(objName, out var oldPos)) {
                string oldCell = GetCellKey(oldPos.x, oldPos.y);
                if (_gridCells.ContainsKey(oldCell)) _gridCells[oldCell].Remove(objName);
            }
            Register(objName, newX, newY);
        }

        public static void Unregister(string objName) {
            if (_entityPositions.TryGetValue(objName, out var pos)) {
                string cellKey = GetCellKey(pos.x, pos.y);
                if (_gridCells.ContainsKey(cellKey)) _gridCells[cellKey].Remove(objName);
                _entityPositions.Remove(objName);
            }
        }

        // 【God Mode Efun】O(1) 複雜度的 AOE 範圍查詢
        public static List<string> GetObjectsInRadius(int centerX, int centerY, int radius) {
            var result = new List<string>();
            int minCellX = (centerX - radius) / CELL_SIZE;
            int maxCellX = (centerX + radius) / CELL_SIZE;
            int minCellY = (centerY - radius) / CELL_SIZE;
            int maxCellY = (centerY + radius) / CELL_SIZE;

            for (int cx = minCellX; cx <= maxCellX; cx++) {
                for (int cy = minCellY; cy <= maxCellY; cy++) {
                    string cellKey = $"{cx}_{cy}";
                    if (_gridCells.TryGetValue(cellKey, out var entities)) {
                        foreach (var obj in entities) {
                            var pos = _entityPositions[obj];
                            int dx = pos.x - centerX;
                            int dy = pos.y - centerY;
                            if (dx * dx + dy * dy <= radius * radius) {
                                result.Add(obj);
                            }
                        }
                    }
                }
            }
            return result;
        }
    }
}
"""

with open("LithosNet.VM/GridMapManager.cs", "w", encoding="utf-8") as f:
    f.write(grid_code)
print("✅ GridMapManager.cs (MMORPG 空間索引) 已建立！")

# 2. 注入 MMORPG Efun 到 BuiltInEfuns.cs
with open("LithosNet.VM/BuiltInEfuns.cs", "r", encoding="utf-8") as f:
    efun_content = f.read()

mmorpg_efuns = """
        // 【MMORPG Efun】註冊實體到空間網格
        [Efun("map_register")]
        public static LpcValue MapRegister(LpcValue[] args) {
            if (args.Length >= 3) GridMapManager.Register(args[0].AsString(), args[1].AsInt(), args[2].AsInt());
            return LpcValue.Create(1);
        }

        // 【MMORPG Efun】移動實體
        [Efun("map_move")]
        public static LpcValue MapMove(LpcValue[] args) {
            if (args.Length >= 3) GridMapManager.Move(args[0].AsString(), args[1].AsInt(), args[2].AsInt());
            return LpcValue.Create(1);
        }

        // 【MMORPG Efun】AOE 範圍查詢 (效能碾壓 rAthena 的核心)
        [Efun("get_objects_in_radius")]
        public static LpcValue GetObjectsInRadius(LpcValue[] args) {
            if (args.Length < 3) return LpcValue.Create(new List<LpcValue>());
            var list = GridMapManager.GetObjectsInRadius(args[0].AsInt(), args[1].AsInt(), args[2].AsInt());
            var lpcList = new List<LpcValue>();
            foreach(var s in list) lpcList.Add(LpcValue.Create(s));
            return LpcValue.Create(lpcList);
        }
"""

if "[Efun(\"map_register\")]" not in efun_content:
    efun_content = efun_content.rstrip()
    if efun_content.endswith("}"):
        efun_content = efun_content[:-1] + mmorpg_efuns + "\n    }\n"
    with open("LithosNet.VM/BuiltInEfuns.cs", "w", encoding="utf-8") as f:
        f.write(efun_content)
    print("✅ BuiltInEfuns.cs 已注入 MMORPG 空間 Efun！")

