import re

with open('./LithosNet.Compiler/Grammar/LPC.g4', 'r', encoding='utf-8') as f:
    g4 = f.read()

# 精準鎖定 assignmentExpr 規則並重寫為無歧義的右遞迴
match = re.search(r'(assignmentExpr\s*\n?\s*:\s*)([^;]+);', g4, re.DOTALL)
if match:
    # 【終極語法】左邊強制是 postfixExpr，後面跟著可選的賦值運算子！
    new_rule = "postfixExpr ((ASSIGN | PLUS_ASSIGN | MINUS_ASSIGN) assignmentExpr)?"
    g4 = g4.replace(match.group(0), f'assignmentExpr\n    : {new_rule}\n    ;')
    
    with open('./LithosNet.Compiler/Grammar/LPC.g4', 'w', encoding='utf-8') as f:
        f.write(g4)
    print("✅ assignmentExpr 已恢復無歧義右遞迴！歧義性徹底消滅！")
else:
    print("⚠ 找不到 assignmentExpr 規則！")
