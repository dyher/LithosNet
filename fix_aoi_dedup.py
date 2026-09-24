with open("LithosNet.VM/GridMapManager.cs", "r", encoding="utf-8") as f:
    grid_content = f.read()

old_logic = """            // 【AOI Push】通知周圍實體
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
            }"""

new_logic = """            // 【AOI Push】通知周圍實體 (使用 HashSet 去重，確保每個實體只被通知一次)
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
            }"""

if old_logic in grid_content:
    grid_content = grid_content.replace(old_logic, new_logic)
    with open("LithosNet.VM/GridMapManager.cs", "w", encoding="utf-8") as f:
        f.write(grid_content)
    print("✅ GridMapManager.cs: AOI 重複通知已使用 HashSet 完美去重！")
else:
    print("ℹ️ 邏輯已更新或未找到目標")
