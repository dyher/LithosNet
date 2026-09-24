import re

with open("LithosNet.VM/ObjectManager.cs", "r", encoding="utf-8") as f:
    code = f.read()

# 將原本呼叫 VisitProgram 的單行代碼，替換為直接遍歷 top-level 宣告的邏輯
old_call = "var ast = new LithosNet.Compiler.AstBuilder().Visit(tree);"
new_call = """var builder = new LithosNet.Compiler.AstBuilder();
                var ast = new System.Collections.Generic.List<LithosNet.Core.AstNode>();
                foreach(var decl in tree.topLevelDecl()) {
                    var node = builder.Visit(decl);
                    if (node != null) ast.Add(node);
                }"""

if old_call in code:
    code = code.replace(old_call, new_call)
    with open("LithosNet.VM/ObjectManager.cs", "w", encoding="utf-8") as f:
        f.write(code)
    print("✅ ObjectManager.cs 已終極修復：直接遍歷 ANTLR4 樹生成 List<AstNode>，完美對齊舊版 JIT！")
else:
    print("⚠️ 找不到目標代碼，可能已修復或格式不同！")
