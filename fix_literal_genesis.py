import re

# 1. 修改 LPC.g4
with open('./LithosNet.Compiler/Grammar/LPC.g4', 'r', encoding='utf-8') as f:
    g4 = f.read()

# 自動尋找包含 INT_LITERAL 和 STRING_LITERAL 的基礎規則 (可能是 atom, primary, primaryExpr)
atom_pattern = r'([a-zA-Z_]+)\s*:\s*[^;]*INT_LITERAL[^;]*STRING_LITERAL[^;]*;'
match = re.search(atom_pattern, g4, re.DOTALL)

if match and 'arrayLiteral' not in g4:
    rule_name = match.group(1)
    print(f"🔍 找到 Literal 宿主規則: {rule_name}")
    
    # 在該規則中加入 arrayLiteral 和 mappingLiteral
    new_rule = match.group(0).replace(';', '\n    | arrayLiteral\n    | mappingLiteral\n    ;')
    g4 = g4.replace(match.group(0), new_rule)
    
    # 在檔案末尾加入新的語法規則定義
    new_rules = """
arrayLiteral
    : LPAREN LBRACE (expr (COMMA expr)*)? RBRACE RPAREN
    ;

mappingLiteral
    : LPAREN LBRACKET (expr COLON expr (COMMA expr COLON expr)*)? RBRACKET RPAREN
    ;
"""
    g4 = g4.rstrip() + "\n" + new_rules
    
    with open('./LithosNet.Compiler/Grammar/LPC.g4', 'w', encoding='utf-8') as f:
        f.write(g4)
    print("✅ LPC.g4 已補齊 Array 與 Mapping Literal 創世法典！")
else:
    print("ℹ LPC.g4 已包含 Literal 規則或找不到宿主。")

# 2. 修改 AstBuilder.cs
with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    builder = f.read()

if 'VisitArrayLiteral' not in builder:
    visitors = """
        public override AstNode VisitArrayLiteral(LPCParser.ArrayLiteralContext context) {
            var node = new ArrayLiteralNode();
            if (context.expr() != null) {
                foreach (var e in context.expr()) {
                    var astNode = Visit(e);
                    if (astNode != null) node.Elements.Add(astNode);
                }
            }
            return node;
        }

        public override AstNode VisitMappingLiteral(LPCParser.MappingLiteralContext context) {
            var node = new MappingLiteralNode();
            if (context.expr() != null) {
                var exprs = context.expr();
                for (int i = 0; i < exprs.Length; i += 2) {
                    var keyNode = Visit(exprs[i]);
                    var valNode = (i + 1 < exprs.Length) ? Visit(exprs[i + 1]) : null;
                    if (keyNode != null) node.Keys.Add(keyNode);
                    if (valNode != null) node.Values.Add(valNode);
                }
            }
            return node;
        }
"""
    last_brace = builder.rfind('}')
    builder = builder[:last_brace] + visitors + builder[last_brace:]
    
    with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
        f.write(builder)
    print("✅ AstBuilder.cs 已注入 VisitArrayLiteral 與 VisitMappingLiteral！")
else:
    print("ℹ AstBuilder.cs 已包含 Visit 方法。")
