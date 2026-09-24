import re

# 1. 升級 GridMapManager.cs (加入九宮格 AOI 視野派發)
with open("LithosNet.VM/GridMapManager.cs", "r", encoding="utf-8") as f:
    grid_content = f.read()

aoi_logic = """
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

            // 【AOI Push】通知周圍實體
            foreach (var cell in enterCells) {
                if (_gridCells.TryGetValue(cell, out var entities)) {
                    foreach (var other in entities) {
                        if (other != objName) {
                            var pos = _entityPositions[other];
                            try { objMgr.CallFunction(other, "aoi_enter", LpcValue.Create(objName), LpcValue.Create(newX), LpcValue.Create(newY)); } catch {}
                            try { objMgr.CallFunction(objName, "aoi_enter", LpcValue.Create(other), LpcValue.Create(pos.x), LpcValue.Create(pos.y)); } catch {}
                        }
                    }
                }
            }

            foreach (var cell in leaveCells) {
                if (_gridCells.TryGetValue(cell, out var entities)) {
                    foreach (var other in entities) {
                        if (other != objName) {
                            try { objMgr.CallFunction(other, "aoi_leave", LpcValue.Create(objName)); } catch {}
                            try { objMgr.CallFunction(objName, "aoi_leave", LpcValue.Create(other)); } catch {}
                        }
                    }
                }
            }
        }
"""

if "MoveWithAOI" not in grid_content:
    grid_content = grid_content.replace("public static void Unregister", aoi_logic + "\n        public static void Unregister")
    with open("LithosNet.VM/GridMapManager.cs", "w", encoding="utf-8") as f:
        f.write(grid_content)
    print("✅ GridMapManager.cs: 九宮格 AOI 視野派發已注入！")

# 2. 升級 BuiltInEfuns.cs (更新 map_move 並注入 Fiber task_sleep)
with open("LithosNet.VM/BuiltInEfuns.cs", "r", encoding="utf-8") as f:
    efun_content = f.read()

# 替換原本的 map_move 為 AOI 版本
efun_content = efun_content.replace(
    "GridMapManager.Move(args[0].AsString(), args[1].AsInt(), args[2].AsInt());",
    "GridMapManager.MoveWithAOI(ObjectManager.Instance, args[0].AsString(), args[1].AsInt(), args[2].AsInt());"
)

fiber_efun = """
        // 【MMORPG Fiber】非阻塞式異步延遲 (不卡死主線程)
        [Efun("task_sleep")]
        public static LpcValue TaskSleep(LpcValue[] args) {
            if (args.Length < 2) return LpcValue.Create(0);
            int ms = args[0].AsInt();
            string callback = args[1].AsString();
            string targetObj = SessionManager.CurrentPlayer.Value ?? "";
            
            // 利用 C# 原生的 Task.Delay 實現非阻塞掛起
            System.Threading.Tasks.Task.Run(async () => {
                await System.Threading.Tasks.Task.Delay(ms);
                if (ObjectManager.Instance.ObjectExists(targetObj)) {
                    ObjectManager.Instance.CallFunction(targetObj, callback);
                }
            });
            return LpcValue.Create(1);
        }
"""

if "[Efun(\"task_sleep\")]" not in efun_content:
    efun_content = efun_content.replace("public static class BuiltInEfuns {", "public static class BuiltInEfuns {\n" + fiber_efun)
    with open("LithosNet.VM/BuiltInEfuns.cs", "w", encoding="utf-8") as f:
        f.write(efun_content)
    print("✅ BuiltInEfuns.cs: AOI map_move 與 Fiber task_sleep 已注入！")

# 3. 確保 ObjectManager 有 Instance 單例 (供 Efun 呼叫)
with open("LithosNet.VM/ObjectManager.cs", "r", encoding="utf-8") as f:
    om_content = f.read()
if "public static ObjectManager Instance" not in om_content:
    om_content = om_content.replace("public class ObjectManager {", "public class ObjectManager {\n        public static ObjectManager Instance { get; private set; }\n        public ObjectManager() { Instance = this; }")
    with open("LithosNet.VM/ObjectManager.cs", "w", encoding="utf-8") as f:
        f.write(om_content)
    print("✅ ObjectManager.cs: 注入 Instance 單例！")

