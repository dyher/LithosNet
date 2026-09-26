with open('LithosNet.VM/Interpreter.cs', 'r', encoding='utf-8') as f:
    code = f.read()

if '[IndexAssign X-Ray]' not in code:
    old_logic = '''case IndexAssignmentNode ia:
                    var col = Eval(ia.Array); var idx = Eval(ia.Index); var val = Eval(ia.Value);
                    if (col.Type == LpcType.Array) col.AsArray()[idx.AsInt()] = val;
                    else if (col.Type == LpcType.Mapping) col.AsMapping()[idx.AsString()] = val;
                    break;'''
    
    new_logic = '''case IndexAssignmentNode ia:
                    var col = Eval(ia.Array); var idx = Eval(ia.Index); var val = Eval(ia.Value);
                    Console.WriteLine($"🔍 [IndexAssign X-Ray] Target Type: {col.Type}, Index: '{idx.AsString()}', Val Type: {val.Type}");
                    if (col.Type == LpcType.Array) col.AsArray()[idx.AsInt()] = val;
                    else if (col.Type == LpcType.Mapping) {
                        var map = col.AsMapping();
                        Console.WriteLine($"🔍 [IndexAssign X-Ray] Map Keys BEFORE: {string.Join(", ", map.Keys)}");
                        map[idx.AsString()] = val;
                        Console.WriteLine($"🔍 [IndexAssign X-Ray] Map Keys AFTER: {string.Join(", ", map.Keys)}");
                    }
                    break;'''
    
    code = code.replace(old_logic, new_logic)
    with open('LithosNet.VM/Interpreter.cs', 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ Interpreter.cs 已注入 IndexAssign X-Ray！")
else:
    print("ℹ X-Ray 已存在。")
