import re

with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# 精準鎖定 VisitBlock 中的 foreach 迴圈
old_loop = r'foreach\(var stmt in context\.statement\(\)\) \{\s*var node = Visit\(stmt\);\s*if \(node != null\) stmts\.Add\(node\);\s*\}'

new_loop = '''foreach(var stmt in context.statement()) {
                    var node = Visit(stmt);
                    Console.WriteLine($"🔍 [Block X-Ray] ANTLR Stmt: {stmt.GetType().Name.Replace("Context", "")}, AST Node: {(node != null ? node.GetType().Name : "NULL")}");
                    if (node != null) stmts.Add(node);
                }'''

if '[Block X-Ray]' not in code:
    code = re.sub(old_loop, new_loop, code, flags=re.DOTALL)
    with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ VisitBlock 已注入 Block X-Ray！真相即將大白！")
else:
    print("ℹ️ 已存在 Block X-Ray。")
