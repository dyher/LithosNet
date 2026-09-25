import re

with open('./LithosNet.Compiler/Grammar/LPC.g4', 'r', encoding='utf-8') as f:
    g4 = f.read()

# 1. 在 Lexer 區域注入融合 Token (必須放在 LPAREN 之前，確保優先匹配！)
lexer_injection = """
ARRAY_OPEN  : '({' ;
ARRAY_CLOSE : '})' ;
MAP_OPEN    : '([' ;
MAP_CLOSE   : '])' ;
"""
if 'MAP_OPEN' not in g4:
    # 精準插入到 LPAREN 定義的前方
    g4 = re.sub(r'(LPAREN\s*:\s*\'\(\')', lexer_injection + r'\1', g4)
    print("✅ Lexer 融合 Token (MAP_OPEN, MAP_CLOSE) 已注入！")

# 2. 強制重寫 Parser 規則，使用新的融合 Token
g4 = re.sub(
    r'arrayLiteral\s*:[^;]+;',
    'arrayLiteral : ARRAY_OPEN (expr (COMMA expr)*)? ARRAY_CLOSE ;',
    g4
)
g4 = re.sub(
    r'mappingLiteral\s*:[^;]+;',
    'mappingLiteral : MAP_OPEN (expr COLON expr (COMMA expr COLON expr)*)? MAP_CLOSE ;',
    g4
)
print("✅ Parser 規則已同步使用融合 Token！")

with open('./LithosNet.Compiler/Grammar/LPC.g4', 'w', encoding='utf-8') as f:
    f.write(g4)
