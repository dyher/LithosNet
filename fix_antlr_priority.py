import re

with open('./LithosNet.Compiler/Grammar/LPC.g4', 'r', encoding='utf-8') as f:
    g4 = f.read()

# 1. 先無情拔除原本位置的 arrayLiteral 和 mappingLiteral
g4 = re.sub(r'\|\s*arrayLiteral\s*', '', g4)
g4 = re.sub(r'\|\s*mappingLiteral\s*', '', g4)

# 2. 將它們強制插入到 primaryExpr 的最頂端 (最高優先級！)
g4 = re.sub(
    r'(primaryExpr\s*:\s*)', 
    r'\1arrayLiteral\n    | mappingLiteral\n    | ', 
    g4
)

with open('./LithosNet.Compiler/Grammar/LPC.g4', 'w', encoding='utf-8') as f:
    f.write(g4)
print("✅ LPC.g4 優先級已重置！Literal 規則現在擁有最高匹配權！")
