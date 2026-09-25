import re

with open('LithosNet.VM/Interpreter.cs', 'r', encoding='utf-8') as f:
    interp = f.read()

# 1. 在 AssignmentNode 注入 X-Ray
if '[VM Type X-Ray] Assign' not in interp:
    interp = interp.replace(
        'case AssignmentNode a:',
        '''case AssignmentNode a: 
                    var assignVal = Eval(a.Value);
                    Console.WriteLine($"🔍 [VM Type X-Ray] Assign '{a.VariableName}' -> LpcType: {assignVal.Type}, CLR: {assignVal.Value?.GetType().Name}");'''
    )
    interp = interp.replace(
        '_scope.Set(a.VariableName, Eval(a.Value));',
        '_scope.Set(a.VariableName, assignVal);'
    )

# 2. 在 IndexAssignmentNode 注入 X-Ray
if '[VM Type X-Ray] IndexAssign' not in interp:
    interp = interp.replace(
        'case IndexAssignmentNode ia:',
        '''case IndexAssignmentNode ia:
                    var iaCol = Eval(ia.Array); var iaIdx = Eval(ia.Index); var iaVal = Eval(ia.Value);
                    Console.WriteLine($"🔍 [VM Type X-Ray] IndexAssign -> Target LpcType: {iaCol.Type}, CLR: {iaCol.Value?.GetType().Name}, Index: {iaIdx.AsString()}");'''
    )
    interp = interp.replace(
        'var col = Eval(ia.Array); var idx = Eval(ia.Index); var val = Eval(ia.Value);',
        'var col = iaCol; var idx = iaIdx; var val = iaVal;'
    )

with open('LithosNet.VM/Interpreter.cs', 'w', encoding='utf-8') as f:
    f.write(interp)
print("✅ Interpreter.cs 已注入 VM 底層型別 X-Ray！")
