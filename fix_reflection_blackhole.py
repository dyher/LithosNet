import re

with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# 精準鎖定拆解 BlockNode 的反射邏輯
old_logic = '''var stmtsProp = blockNode.GetType().GetProperty("Statements");
                    if (stmtsProp != null) body = (System.Collections.Generic.List<AstNode>)stmtsProp.GetValue(blockNode);
                    else body.Add(blockNode); // Fallback'''

new_logic = '''var stmtsProp = blockNode.GetType().GetProperty("Statements");
                    var stmtsField = blockNode.GetType().GetField("Statements");
                    if (stmtsProp != null) body = (System.Collections.Generic.List<AstNode>)stmtsProp.GetValue(blockNode);
                    else if (stmtsField != null) body = (System.Collections.Generic.List<AstNode>)stmtsField.GetValue(blockNode);
                    else body.Add(blockNode); // Fallback'''

if 'stmtsField' not in code:
    code = code.replace(old_logic, new_logic)
    
    # 備用暴力替換 (防止格式微調)
    if 'stmtsField' not in code:
        code = code.replace(
            'var stmtsProp = blockNode.GetType().GetProperty("Statements");',
            'var stmtsProp = blockNode.GetType().GetProperty("Statements");\n                    var stmtsField = blockNode.GetType().GetField("Statements");'
        )
        code = code.replace(
            'if (stmtsProp != null) body = (System.Collections.Generic.List<AstNode>)stmtsProp.GetValue(blockNode);',
            'if (stmtsProp != null) body = (System.Collections.Generic.List<AstNode>)stmtsProp.GetValue(blockNode);\n                    else if (stmtsField != null) body = (System.Collections.Generic.List<AstNode>)stmtsField.GetValue(blockNode);'
        )

    with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ AstBuilder.cs 反射黑洞已修復！完美支援 Field 與 Property 雙重探測！")
else:
    print("ℹ️ 已存在修復邏輯。")
