import re

with open('LithosNet.VM/Interpreter.cs', 'r', encoding='utf-8') as f:
    interp = f.read()

# 1. 替換舊的 call_out Task.Delay 為 CallOutManager.Schedule
old_callout = r'if \(c\.Name == "call_out" && cArgs\.Count >= 2\) \{.*?return LpcValue\.Create\(1\);\s*\}'
new_callout = '''if (c.Name == "call_out" && cArgs.Count >= 2) {
                        string funcName = cArgs[0].AsString(); 
                        int delaySec = cArgs[1].AsInt();
                        var passArgs = cArgs.Skip(2).ToArray(); 
                        int handle = CallOutManager.Schedule(this.ObjectName, funcName, delaySec, passArgs, _objMgr);
                        return LpcValue.Create(handle);
                    }'''
interp = re.sub(old_callout, new_callout, interp, flags=re.DOTALL)

# 2. 替換 destruct，加入 ClearObject 自動清理定時器
old_destruct = r'if \(c\.Name == "destruct" && cArgs\.Count >= 1\) \{ _objMgr\.DestructObject\(cArgs\[0\]\.AsString\(\)\); return LpcValue\.Create\(1\); \}'
new_destruct = '''if (c.Name == "destruct" && cArgs.Count >= 1) { 
                        string target = cArgs[0].AsString();
                        CallOutManager.ClearObject(target);
                        _objMgr.DestructObject(target); 
                        return LpcValue.Create(1); 
                    }'''
interp = re.sub(old_destruct, new_destruct, interp)

# 3. 注入 remove_call_out
if 'c.Name == "remove_call_out"' not in interp:
    interp = interp.replace(
        'if (c.Name == "destruct"',
        'if (c.Name == "remove_call_out" && cArgs.Count >= 1) { return LpcValue.Create(CallOutManager.Remove(this.ObjectName, cArgs[0].AsString())); }\n                    if (c.Name == "destruct"'
    )

with open('LithosNet.VM/Interpreter.cs', 'w', encoding='utf-8') as f:
    f.write(interp)
print("✅ Interpreter.cs 已完美對接 CallOutManager！")
