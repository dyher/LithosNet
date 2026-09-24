#nullable disable
using System;
using System.Collections.Generic;
using LithosNet.Core;

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

        
        // 【MMORPG 靈魂】九宮格 AOI 視野派發
        public static void MoveWithAOI(ObjectManager objMgr, string objName, int newX, int newY) {
            if (!_entityPositions.TryGetValue(objName, out var oldPos)) {
                Register(objName, newX, newY);
                return;
            }

            int oldCX = oldPos.x / CELL_SIZE;
            int oldCY = oldPos.y / CELL_SIZE;
            int newCX = newX / CELL_SIZE;
            int newCY = newY / CELL_SIZE;

            // 如果沒有跨越網格邊界，只更新座標，不觸發 AOI
            if (oldCX == newCX && oldCY == newCY) {
                _entityPositions[objName] = (newX, newY);
                return;
            }

            // 計算舊的九宮格與新的九宮格
            var oldCells = new HashSet<string>();
            var newCells = new HashSet<string>();
            for (int dx = -1; dx <= 1; dx++) {
                for (int dy = -1; dy <= 1; dy++) {
                    oldCells.Add($"{oldCX + dx}_{oldCY + dy}");
                    newCells.Add($"{newCX + dx}_{newCY + dy}");
                }
            }

            // 找出需要通知的差異網格
            var enterCells = new HashSet<string>(newCells);
            enterCells.ExceptWith(oldCells); // 新進入的視野
            
            var leaveCells = new HashSet<string>(oldCells);
            leaveCells.ExceptWith(newCells); // 離開的視野

            // 更新座標
            _entityPositions[objName] = (newX, newY);
            string oldKey = $"{oldCX}_{oldCY}";
            string newKey = $"{newCX}_{newCY}";
            if (_gridCells.ContainsKey(oldKey)) _gridCells[oldKey].Remove(objName);
            if (!_gridCells.ContainsKey(newKey)) _gridCells[newKey] = new HashSet<string>();
            _gridCells[newKey].Add(objName);

            // 【AOI Push】通知周圍實體 (使用 HashSet 去重，確保每個實體只被通知一次)
            var enterEntities = new HashSet<string>();
            foreach (var cell in enterCells) {
                if (_gridCells.TryGetValue(cell, out var entities)) {
                    foreach (var other in entities) if (other != objName) enterEntities.Add(other);
                }
            }
            foreach (var other in enterEntities) {
                var pos = _entityPositions[other];
                try { objMgr.CallFunction(other, "aoi_enter", LpcValue.Create(objName), LpcValue.Create(newX), LpcValue.Create(newY)); } catch {}
                try { objMgr.CallFunction(objName, "aoi_enter", LpcValue.Create(other), LpcValue.Create(pos.x), LpcValue.Create(pos.y)); } catch {}
            }

            var leaveEntities = new HashSet<string>();
            foreach (var cell in leaveCells) {
                if (_gridCells.TryGetValue(cell, out var entities)) {
                    foreach (var other in entities) if (other != objName) leaveEntities.Add(other);
                }
            }
            foreach (var other in leaveEntities) {
                try { objMgr.CallFunction(other, "aoi_leave", LpcValue.Create(objName)); } catch {}
                try { objMgr.CallFunction(objName, "aoi_leave", LpcValue.Create(other)); } catch {}
            }
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
