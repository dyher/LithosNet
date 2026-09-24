import re

with open('LithosNet.Compiler/Parser.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# 注入 Lookahead 到 ParseVariableDeclaration 的開頭
pattern = r'(private\s+AstNode\s+ParseVariableDeclaration\s*\(\s*\)\s*\{)'
replacement = r'''\1
    // 【終極修復】Lookahead 檢查是否為函數宣告
    if (Tokens.Count > _current + 2 && Tokens[_current + 1].Type == TokenType.Identifier && Tokens[_current + 2].Type == TokenType.LeftParen) {
        return ParseFunctionDeclaration();
    }
'''
new_code, count = re.subn(pattern, replacement, code)

if count > 0:
    with open('LithosNet.Compiler/Parser.cs', 'w', encoding='utf-8') as f:
        f.write(new_code)
    print(f"✅ Parser.cs 已成功注入 Lookahead 邏輯！替換了 {count} 處。")
else:
    print("⚠️ 沒有找到 ParseVariableDeclaration，嘗試備用方案...")
