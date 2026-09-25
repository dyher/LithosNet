import re

# 1. 修改 LPC.g4
with open('./LithosNet.Compiler/Grammar/LPC.g4', 'r', encoding='utf-8') as f:
    g4 = f.read()

if 'arrayLiteral' not in g4:
    match = re.search(r'(primaryExpr\s*:[^;]*);', g4, re.DOTALL)
    if match:
        old_rule = match.group(0)
        new_rule = old_rule[:-1] + '\n    | arrayLiteral\n    | mappingLiteral\n    ;'
        g4 = g4.replace(old_rule, new_rule)
        
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
        print("✅ LPC.g4 已成功注入 Literal 規則！")

# 2. 修改 AstBuilder.cs
with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    content = f.read()

if 'VisitArrayLiteral' not in content:
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
    # 【終極修復】找到 class 的結束大括號 (倒數第二個 })，而不是 namespace 的
    last_brace = content.rfind('}')
    second_last_brace = content.rfind('}', 0, last_brace)
    
    content = content[:second_last_brace] + visitors + content[second_last_brace:]
    with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
        f.write(content)
    print("✅ AstBuilder.cs 已成功注入 Visitor 方法 (安全插入 class 內部)！")
