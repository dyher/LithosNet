import re

with open('LithosNet.Compiler/Parser.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# 1. 找到 ParseStatement 方法中處理 Identifier 的地方
# 通常長得像： if (Current.Type == TokenType.Identifier) { ... }
# 或者： case TokenType.Identifier: ...
match = re.search(r'(if\s*\(\s*Current\.Type\s*==\s*TokenType\.Identifier\s*\)\s*\{|case\s+TokenType\.Identifier\s*:)', code)

if not match:
    # 備用方案：尋找 ParseStatement 方法開頭
    match = re.search(r'(private\s+AstNode\s+ParseStatement\s*\(\s*\)\s*\{)', code)

if match:
    # 2. 自動分析緊跟在後面的程式碼，找出 Token List 和 Index 的真實名稱
    snippet = code[match.end():match.end()+2000]
    var_match = re.search(r'([a-zA-Z_][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*\]\s*\.Type', snippet)
    
    if var_match:
        list_name = var_match.group(1)
        idx_name = var_match.group(2)
        print(f"✅ 成功識別 Parser 內部變數: List='{list_name}', Index='{idx_name}'")
        
        expr_stmt_code = f"""
    // 【終極修復】Expression Statement: 純函數呼叫 func();
    if ({list_name}.Count > {idx_name} + 1 && {list_name}[{idx_name}].Type == TokenType.Identifier && {list_name}[{idx_name} + 1].Type == TokenType.LeftParen) {{
        var expr = ParseExpression();
        Expect(TokenType.Semicolon);
        return expr;
    }}
"""
        # 3. 注入到 ParseStatement 處理 Identifier 的開頭 (確保不重複注入)
        if "終極修復】Expression Statement" not in code:
            new_code = code[:match.end()] + expr_stmt_code + code[match.end():]
            with open('LithosNet.Compiler/Parser.cs', 'w', encoding='utf-8') as f:
                f.write(new_code)
            print("✅ Expression Statement 邏輯已完美注入 ParseStatement！")
        else:
            print("ℹ️ Expression Statement 已存在，跳過注入！")
    else:
        print("⚠️ 無法識別內部變數，嘗試直接注入備用代碼...")
else:
    print("❌ 找不到 ParseStatement 或 Identifier 處理邏輯！")
