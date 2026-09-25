import re

# 1. 修改 BuiltInEfuns.cs (在 keys 開頭注入 X-Ray)
with open('LithosNet.VM/BuiltInEfuns.cs', 'r', encoding='utf-8') as f:
    efuns = f.read()

if '[Efun X-Ray] keys()' not in efuns:
    efuns = efuns.replace(
        'public static LpcValue Keys(LpcValue[] args) {',
        'public static LpcValue Keys(LpcValue[] args) {\n            Console.WriteLine($"🔍 [Efun X-Ray] keys() arg Type: {args[0].Type}, CLR Type: {args[0].Value?.GetType().Name}");'
    )
    with open('LithosNet.VM/BuiltInEfuns.cs', 'w', encoding='utf-8') as f:
        f.write(efuns)
    print("✅ BuiltInEfuns.cs 已注入 keys() X-Ray！")

# 2. 修改 Interpreter.cs (在 IndexAssignmentNode 注入 X-Ray)
with open('LithosNet.VM/Interpreter.cs', 'r', encoding='utf-8') as f:
    interp = f.read()

if '[VM X-Ray] IndexAssign' not in interp:
    interp = interp.replace(
        'case IndexAssignmentNode ia:',
        'case IndexAssignmentNode ia:\n                    Console.WriteLine($"🔍 [VM X-Ray] IndexAssign: Target Name={ia.Array.GetType().Name}, col.Type={Eval(ia.Array).Type}");'
    )
    with open('LithosNet.VM/Interpreter.cs', 'w', encoding='utf-8') as f:
        f.write(interp)
    print("✅ Interpreter.cs 已注入 IndexAssignment X-Ray！")
