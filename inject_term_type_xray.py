with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    code = f.read()

if '[Postfix Term X-Ray]' not in code:
    # 在 while 迴圈開始處注入 Term X-Ray
    old_while = '''while (i < context.ChildCount) {
                var child = context.GetChild(i);
                if (child is ITerminalNode term) {'''
    
    new_while = '''while (i < context.ChildCount) {
                var child = context.GetChild(i);
                if (child is ITerminalNode term) {
                    Console.WriteLine($"🔍 [Postfix Term X-Ray] Type={term.Symbol.Type}, Text='{term.GetText()}', LPAREN={LPCParser.LPAREN}, LBRACKET={LPCParser.LBRACKET}");'''
    
    code = code.replace(old_while, new_while)

    # 將 LBRACKET 的處理邏輯用 try/catch 死死包住
    old_lbracket = '''else if (term.Symbol.Type == LPCParser.LBRACKET) {
                        // 【創世補齊】完美處理索引訪問 a[b]！
                        var indexNode = Visit(context.GetChild(i + 1));
                        node = CreateNode("IndexAccessNode", new Dictionary<string, object> { { "Array", node }, { "Target", node }, { "Index", indexNode } });
                        Console.WriteLine($"🔍 [Postfix LBRACKET X-Ray] CreateNode returned: {(node != null ? node.GetType().Name : "NULL")}");
                        i++; // skip expr
                        i++; // skip RBRACKET
                    }'''
    
    new_lbracket = '''else if (term.Symbol.Type == LPCParser.LBRACKET) {
                        try {
                            // 【創世補齊】完美處理索引訪問 a[b]！
                            var indexNode = Visit(context.GetChild(i + 1));
                            node = CreateNode("IndexAccessNode", new Dictionary<string, object> { { "Array", node }, { "Target", node }, { "Index", indexNode } });
                            Console.WriteLine($"🔍 [Postfix LBRACKET X-Ray] CreateNode returned: {(node != null ? node.GetType().Name : "NULL")}");
                            i++; // skip expr
                            i++; // skip RBRACKET
                        } catch (Exception ex) {
                            Console.WriteLine($"❌ [Postfix LBRACKET ERROR] {ex.Message}");
                        }
                    }'''
    
    code = code.replace(old_lbracket, new_lbracket)

    with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ 已注入 Term Type X-Ray 與 Try/Catch！")
else:
    print("ℹ X-Ray 已存在。")
