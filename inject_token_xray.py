import re

with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    builder = f.read()

# 在 VisitPrimaryExpr 的最開頭注入 Token X-Ray
xray_code = '''Console.WriteLine($"🔍 [PrimaryExpr X-Ray] Text: {context.GetText()}, StartType: {context.Start?.Type}, StartText: {context.Start?.Text}");
            '''

if '[PrimaryExpr X-Ray]' not in builder:
    builder = builder.replace(
        'public override AstNode VisitPrimaryExpr(LPCParser.PrimaryExprContext context) {',
        'public override AstNode VisitPrimaryExpr(LPCParser.PrimaryExprContext context) {\n            ' + xray_code
    )
    with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
        f.write(builder)
    print("✅ VisitPrimaryExpr 已注入 Token 級別 X-Ray！")
else:
    print("ℹ X-Ray 已存在。")
