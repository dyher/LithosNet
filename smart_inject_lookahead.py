import re

with open('LithosNet.Compiler/Parser.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# 1. 找出 ParseVariableDeclaration 的開頭
match = re.search(r'(private\s+AstNode\s+ParseVariableDeclaration\s*\(\s*\)\s*\{)', code)
if match:
    # 2. 自動分析緊跟在後面的程式碼，找出 Token List 和 Index 的真實名稱
    # 例如： if (tokens[_position].Type == ...
    snippet = code[match.end():match.end()+1000]
    var_match = re.search(r'([a-zA-Z_][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*\]', snippet)
    
    if var_match:
        list_name = var_match.group(1)
        idx_name = var_match.group(2)
        print(f"✅ 成功識別 Parser 內部變數: List='{list_name}', Index='{idx_name}'")
        
        lookahead_code = f"""
    // 【終極修復】Lookahead 檢查是否為函數宣告
    if ({list_name}.Count > {idx_name} + 2 && {list_name}[{idx_name} + 1].Type == TokenType.Identifier && {list_name}[{idx_name} + 2].Type == TokenType.LeftParen) {{
        return ParseFunctionDeclaration();
    }}
"""
        # 3. 注入到 ParseVariableDeclaration 的開頭 (確保不重複注入)
        if "終極修復" not in code:
            new_code = code[:match.end()] + lookahead_code + code[match.end():]
            with open('LithosNet.Compiler/Parser.cs', 'w', encoding='utf-8') as f:
                f.write(new_code)
            print("✅ Lookahead 邏輯已完美注入！")
        else:
            print("ℹ️ Lookahead 已存在，跳過注入！")
    else:
        print("⚠️ 無法識別內部變數，請手動檢查 Parser.cs！")
else:
    print("⚠️ 找不到 ParseVariableDeclaration！")
