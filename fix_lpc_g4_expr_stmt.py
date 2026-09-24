with open('./LithosNet.Compiler/Grammar/LPC.g4', 'r', encoding='utf-8') as f:
    g4 = f.read()

# 精準在 returnStmt 之後加入 expr ';'
if "expr ';'" not in g4 and "exprStmt" not in g4:
    g4 = g4.replace(
        "| returnStmt",
        "| returnStmt\n    | expr ';'"
    )
    with open('./LithosNet.Compiler/Grammar/LPC.g4', 'w', encoding='utf-8') as f:
        f.write(g4)
    print("✅ LPC.g4 statement 規則已完美補齊 'expr ;'！函數呼叫與賦值將被徹底解放！")
else:
    print("ℹ️ expr ';' 已存在。")
