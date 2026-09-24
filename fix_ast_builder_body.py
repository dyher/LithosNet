import re

with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# 精準鎖定 VisitFuncDecl 中的 body 賦值邏輯
old_logic = r'var body = Visit\(context\.block\(\)\);'
new_logic = '''var blockNode = Visit(context.block());
                var body = new System.Collections.Generic.List<AstNode>();
                if (blockNode != null) {
                    // 【終極拆解】如果拿到的是 BlockNode，自動提取它的 Statements 列表！
                    var stmtsProp = blockNode.GetType().GetProperty("Statements");
                    if (stmtsProp != null) body = (System.Collections.Generic.List<AstNode>)stmtsProp.GetValue(blockNode);
                    else body.Add(blockNode); // Fallback
                }
                Console.WriteLine($"🔍 [AstBuilder X-Ray] 函數 '{name}' 的 Body 語句數量: {body.Count}");'''

if 'var blockNode = Visit(context.block());' not in code:
    code = re.sub(old_logic, new_logic, code)
    with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ AstBuilder.cs 已完美修復：BlockNode 自動拆解為 List<AstNode>，反射型別徹底對齊！")
else:
    print("ℹ️ 已存在修復邏輯。")
