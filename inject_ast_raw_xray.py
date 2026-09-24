import re

with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# 精準鎖定 VisitFuncDecl 中的 X-Ray 輸出
old_xray = r'Console\.WriteLine\(\$"🔍 \[AstBuilder X-Ray\] 函數 \'\{name\}\' 的 Body 語句數量: \{body\.Count\}"\);'
new_xray = '''Console.WriteLine($"🔍 [AstBuilder X-Ray] 函數 '{name}' 的 Body 語句數量: {body.Count}");
                Console.WriteLine($"🔍 [AST-Raw] Block ChildCount: {context.block().ChildCount}");
                var rawText = context.block().GetText();
                Console.WriteLine($"🔍 [AST-Raw] Block Text: {rawText.Substring(0, Math.Min(150, rawText.Length))}...");'''

if '[AST-Raw]' not in code:
    code = re.sub(old_xray, new_xray, code)
    with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ AstBuilder.cs 已注入 AST 原始文本 X-Ray！真相即將大白！")
else:
    print("ℹ️ 已存在 X-Ray。")
