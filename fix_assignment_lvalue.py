with open('./LithosNet.Compiler/Grammar/LPC.g4', 'r', encoding='utf-8') as f:
    g4 = f.read()

# 尋找 assignmentExpr 規則
import re
match = re.search(r'(assignmentExpr\s*\n?\s*:\s*)([^;]+);', g4, re.DOTALL)
if match:
    old_rule = match.group(2)
    print(f"🔍 舊的 assignmentExpr 規則: {old_rule.strip()}")
    
    # 在原有的邏輯前，強制加入：如果左邊是 postfixExpr 且後面有 ASSIGN，則匹配為賦值！
    # 我們直接把原本的邏輯替換為更強大的版本
    new_rule = """
    postfixExpr ASSIGN assignmentExpr
    | postfixExpr PLUS_ASSIGN assignmentExpr
    | postfixExpr MINUS_ASSIGN assignmentExpr
    | logicalOrExpr
    """
    # 注意：為了避免破壞原有的優先級，我們把新規則放在最前面
    g4 = g4.replace(match.group(0), f'assignmentExpr\n    :{new_rule};')
    
    with open('./LithosNet.Compiler/Grammar/LPC.g4', 'w', encoding='utf-8') as f:
        f.write(g4)
    print("✅ assignmentExpr 規則已強制修改！postfixExpr 現在可以作為賦值左值了！")
else:
    print("⚠ 找不到 assignmentExpr 規則！")
