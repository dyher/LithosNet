import re

with open('LithosNet.VM/Interpreter.cs', 'r', encoding='utf-8') as f:
    interp = f.read()

# 替換舊的單維度 move 為 3D move_object 與 get_objects_in_radius
old_move = r'if \(c\.Name == "move" && cArgs\.Count >= 1\) \{ _objMgr\.MoveObject\(this\.ObjectName, cArgs\[0\]\.AsString\(\)\); return LpcValue\.Create\(1\); \}'
new_space_logic = '''if (c.Name == "move" || c.Name == "move_object") { 
                        if (cArgs.Count >= 4) { SpaceManager.Move(cArgs[0].AsString(), cArgs[1].AsInt(), cArgs[2].AsInt(), cArgs[3].AsInt()); }
                        else if (cArgs.Count >= 3) { SpaceManager.Move(this.ObjectName, cArgs[0].AsInt(), cArgs[1].AsInt(), cArgs[2].AsInt()); }
                        return LpcValue.Create(1); 
                    }
                    if (c.Name == "get_objects_in_radius" && cArgs.Count >= 4) {
                        var list = SpaceManager.GetObjectsInRadius(cArgs[0].AsInt(), cArgs[1].AsInt(), cArgs[2].AsInt(), cArgs[3].AsInt());
                        var lpcList = new System.Collections.Generic.List<LpcValue>(); 
                        foreach(var o in list) lpcList.Add(LpcValue.Create(o));
                        return LpcValue.Create(lpcList);
                    }'''

if 'SpaceManager.Move' not in interp:
    interp = re.sub(old_move, new_space_logic, interp)
    with open('LithosNet.VM/Interpreter.cs', 'w', encoding='utf-8') as f:
        f.write(interp)
    print("✅ Interpreter.cs 已注入 3D 空間 Efun！")
else:
    print("ℹ 空間 Efun 已存在。")
