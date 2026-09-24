import re

# 1. 恢復 Ast.cs 為 List<ParameterNode>
with open('LithosNet.Core/Ast.cs', 'r', encoding='utf-8') as f:
    ast = f.read()
ast = ast.replace(
    'public System.Collections.Generic.List<string> Parameters = new System.Collections.Generic.List<string>();', 
    'public System.Collections.Generic.List<ParameterNode> Parameters = new System.Collections.Generic.List<ParameterNode>();'
)
with open('LithosNet.Core/Ast.cs', 'w', encoding='utf-8') as f:
    f.write(ast)
print("✅ Ast.cs 已恢復為 List<ParameterNode>！")

# 2. 恢復 Interpreter.cs 為 func.Parameters[i].Name
with open('LithosNet.VM/Interpreter.cs', 'r', encoding='utf-8') as f:
    interp = f.read()
interp = interp.replace(
    '_scope.Set(func.Parameters[i], args[i]);', 
    '_scope.Set(func.Parameters[i].Name, args[i]);'
)
with open('LithosNet.VM/Interpreter.cs', 'w', encoding='utf-8') as f:
    f.write(interp)
print("✅ Interpreter.cs 已恢復為讀取 .Name！")

# 3. 精準修改 AstBuilder.cs：直接 new ParameterNode()，放棄反射！
with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    builder = f.read()

# 替換參數構建邏輯
old_logic = r'var parameters = new List<string>\(\);\s*if \(context\.paramList\(\) != null && context\.paramList\(\)\.ID\(\) != null\) \{\s*foreach\(var id in context\.paramList\(\)\.ID\(\)\) parameters\.Add\(id\.GetText\(\)\);\s*\}'
new_logic = '''var parameters = new System.Collections.Generic.List<ParameterNode>();
            if (context.paramList() != null && context.paramList().ID() != null) {
                foreach(var id in context.paramList().ID()) {
                    var p = new ParameterNode();
                    p.Name = id.GetText();
                    p.TypeName = "mixed";
                    parameters.Add(p);
                }
            }'''

builder = re.sub(old_logic, new_logic, builder, flags=re.DOTALL)

# 備用暴力替換 (防止正則沒匹配到)
if 'new ParameterNode()' not in builder:
    builder = builder.replace('var parameters = new List<string>();', 'var parameters = new System.Collections.Generic.List<ParameterNode>();')
    builder = builder.replace('parameters.Add(id.GetText());', 'var p = new ParameterNode(); p.Name = id.GetText(); p.TypeName = "mixed"; parameters.Add(p);')

with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
    f.write(builder)
print("✅ AstBuilder.cs 已精準創建 ParameterNode 物件！徹底繞過反射陷阱！")
