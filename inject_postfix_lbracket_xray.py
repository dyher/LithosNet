with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    code = f.read()

if '[Postfix LBRACKET X-Ray]' not in code:
    code = code.replace(
        'node = CreateNode("IndexAccessNode", new Dictionary<string, object> { { "Array", node }, { "Target", node }, { "Index", indexNode } });',
        '''node = CreateNode("IndexAccessNode", new Dictionary<string, object> { { "Array", node }, { "Target", node }, { "Index", indexNode } });
                        Console.WriteLine($"🔍 [Postfix LBRACKET X-Ray] CreateNode returned: {(node != null ? node.GetType().Name : "NULL")}");'''
    )
    with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ 已注入 Postfix LBRACKET X-Ray！")
else:
    print("ℹ X-Ray 已存在。")
