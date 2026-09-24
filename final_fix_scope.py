# 1. 修改 Scope.cs：Get 找不到變數時返回 0（FluffOS 標準行為）
with open('LithosNet.VM/Scope.cs', 'r', encoding='utf-8') as f:
    scope = f.read()

scope = scope.replace(
    'throw new Exception($"[VM] Variable \'{name}\' not found.");',
    'return LpcValue.Create(0); // FluffOS 寬容模式：未定義變數預設為 0'
)

with open('LithosNet.VM/Scope.cs', 'w', encoding='utf-8') as f:
    f.write(scope)
print("✅ Scope.Get 已改為寬容模式！")

# 2. 修改 Interpreter.cs：在 AssignmentNode 和 VariableDeclarationNode 加入 X 光
with open('LithosNet.VM/Interpreter.cs', 'r', encoding='utf-8') as f:
    interp = f.read()

interp = interp.replace(
    'case AssignmentNode a: _scope.Set(a.VariableName, Eval(a.Value)); break;',
    'case AssignmentNode a: Console.WriteLine($"🔍 [Scope] Assign: \'{a.VariableName}\'"); _scope.Set(a.VariableName, Eval(a.Value)); break;'
)

interp = interp.replace(
    'case VariableDeclarationNode v: _scope.Set(v.VariableName, v.Initializer != null ? Eval(v.Initializer) : LpcValue.Create(0)); break;',
    'case VariableDeclarationNode v: Console.WriteLine($"🔍 [Scope] Decl: \'{v.VariableName}\'"); _scope.Set(v.VariableName, v.Initializer != null ? Eval(v.Initializer) : LpcValue.Create(0)); break;'
)

with open('LithosNet.VM/Interpreter.cs', 'w', encoding='utf-8') as f:
    f.write(interp)
print("✅ Interpreter.cs 已注入 X 光變數追蹤！")
