with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    code = f.read()

if '[Assign AST X-Ray]' not in code:
    code = code.replace(
        'var left = Visit(context.logicalOrExpr());',
        'var left = Visit(context.logicalOrExpr());\n                Console.WriteLine($"🔍 [Assign AST X-Ray] left Type: {left?.GetType().Name}, Text: {context.logicalOrExpr().GetText()}");'
    )
    with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ 已注入 Assign AST X-Ray！")
else:
    print("ℹ X-Ray 已存在。")
