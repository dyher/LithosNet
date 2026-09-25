import re

# 1. AstBuilder.cs
with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    builder = f.read()

if '[AST X-Ray] VisitMappingLiteral' not in builder:
    builder = builder.replace(
        'public override AstNode VisitMappingLiteral(LPCParser.MappingLiteralContext context) {',
        'public override AstNode VisitMappingLiteral(LPCParser.MappingLiteralContext context) {\n            Console.WriteLine($"🔍 [AST X-Ray] VisitMappingLiteral called! Expr count: {(context.expr() != null ? context.expr().Length : 0)}");'
    )
    builder = builder.replace(
        'public override AstNode VisitArrayLiteral(LPCParser.ArrayLiteralContext context) {',
        'public override AstNode VisitArrayLiteral(LPCParser.ArrayLiteralContext context) {\n            Console.WriteLine($"🔍 [AST X-Ray] VisitArrayLiteral called! Expr count: {(context.expr() != null ? context.expr().Length : 0)}");'
    )
    with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
        f.write(builder)
    print("✅ AstBuilder.cs 已注入 AST X-Ray！")

# 2. Interpreter.cs
with open('LithosNet.VM/Interpreter.cs', 'r', encoding='utf-8') as f:
    interp = f.read()

if '[VM X-Ray] Evaluating MappingLiteralNode' not in interp:
    interp = interp.replace(
        'case MappingLiteralNode ml:',
        'case MappingLiteralNode ml:\n                    Console.WriteLine($"🔍 [VM X-Ray] Evaluating MappingLiteralNode with {ml.Keys.Count} keys!");'
    )
    interp = interp.replace(
        'case ArrayLiteralNode al:',
        'case ArrayLiteralNode al:\n                    Console.WriteLine($"🔍 [VM X-Ray] Evaluating ArrayLiteralNode with {al.Elements.Count} elements!");'
    )
    with open('LithosNet.VM/Interpreter.cs', 'w', encoding='utf-8') as f:
        f.write(interp)
    print("✅ Interpreter.cs 已注入 VM X-Ray！")
