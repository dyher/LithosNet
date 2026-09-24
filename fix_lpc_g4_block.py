import re

with open('LithosNet.Compiler/Ast/LithosNet.Compiler/Grammar/LPC.g4', 'r', encoding='utf-8') as f:
    g4 = f.read()

# 尋找 block 規則，強制將其改為 statement* (允許 0 到多個)
# 匹配類似 block : '{' statement '}' ; 或 block : '{' statement+ '}' ;
old_pattern = r"block\s*:\s*'\{'\s*statement[\+\*]?\s*'\}'\s*;"
new_rule = "block : '{' statement* '}' ;"

if re.search(old_pattern, g4):
    g4 = re.sub(old_pattern, new_rule, g4)
    with open('LithosNet.Compiler/Ast/LithosNet.Compiler/Grammar/LPC.g4', 'w', encoding='utf-8') as f:
        f.write(g4)
    print("✅ LPC.g4 的 block 規則已完美修復為 statement*！ANTLR 將徹底解放所有語句！")
else:
    print("⚠️ 找不到標準的 block 規則，可能格式特殊。")
    # 暴力替換備案
    g4 = g4.replace("block : '{' statement '}' ;", "block : '{' statement* '}' ;")
    g4 = g4.replace("block : '{' statement+ '}' ;", "block : '{' statement* '}' ;")
    with open('LithosNet.Compiler/Ast/LithosNet.Compiler/Grammar/LPC.g4', 'w', encoding='utf-8') as f:
        f.write(g4)
