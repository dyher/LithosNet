with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    code = f.read()

if '[Postfix X-Ray]' not in code:
    xray_code = '''Console.WriteLine($"🔍 [Postfix X-Ray] Text: {context.GetText()}, ChildCount: {context.ChildCount}");
            for (int _i = 0; _i < context.ChildCount; _i++) {
                var _c = context.GetChild(_i);
                Console.WriteLine($"  -> Child {_i}: Type={_c.GetType().Name}, Text={_c.GetText()}");
            }
            '''
    code = code.replace(
        'public override AstNode VisitPostfixExpr(LPCParser.PostfixExprContext context) {',
        'public override AstNode VisitPostfixExpr(LPCParser.PostfixExprContext context) {\n            ' + xray_code
    )
    with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ VisitPostfixExpr 已注入 Token 級別 X-Ray！")
else:
    print("ℹ X-Ray 已存在。")
