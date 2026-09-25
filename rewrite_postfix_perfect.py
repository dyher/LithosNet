import re

with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# 精準鎖定並重寫整個 VisitPostfixExpr 方法
start_idx = code.find('public override AstNode VisitPostfixExpr')
if start_idx != -1:
    brace_count = 0
    in_method = False
    end_idx = start_idx
    for i in range(start_idx, len(code)):
        if code[i] == '{':
            brace_count += 1
            in_method = True
        elif code[i] == '}':
            brace_count -= 1
            if in_method and brace_count == 0:
                end_idx = i + 1
                break

    new_method = '''public override AstNode VisitPostfixExpr(LPCParser.PostfixExprContext context) {
            AstNode node = Visit(context.primaryExpr());
            int i = 1;
            // 【終極循環】完美處理所有後綴操作 (函數呼叫、Call Other、索引訪問)
            while (i < context.ChildCount) {
                var child = context.GetChild(i);
                if (child is ITerminalNode term) {
                    if (term.Symbol.Type == LPCParser.LPAREN) {
                        var args = new System.Collections.Generic.List<AstNode>();
                        if (i + 1 < context.ChildCount && context.GetChild(i + 1) is LPCParser.ArgListContext al) {
                            if (al.expr() != null) foreach(var e in al.expr()) args.Add(Visit(e));
                            i++; // skip argList
                        }
                        string funcName = "unknown";
                        if (node != null) {
                            var nameProp = node.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            var nameField = node.GetType().GetField("Name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (nameProp != null) funcName = nameProp.GetValue(node)?.ToString() ?? "unknown";
                            else if (nameField != null) funcName = nameField.GetValue(node)?.ToString() ?? "unknown";
                        }
                        node = CreateNode("FunctionCallNode", new Dictionary<string, object> { { "Name", funcName }, { "Arguments", args }, { "Args", args } });
                        i++; // skip RPAREN
                    } else if (term.Symbol.Type == LPCParser.ARROW) {
                        string funcName = context.GetChild(i + 1).GetText();
                        i++; // skip ID
                        var args = new System.Collections.Generic.List<AstNode>();
                        if (i + 1 < context.ChildCount && context.GetChild(i + 1) is ITerminalNode n2 && n2.Symbol.Type == LPCParser.LPAREN) {
                            if (i + 2 < context.ChildCount && context.GetChild(i + 2) is LPCParser.ArgListContext al) {
                                if (al.expr() != null) foreach(var e in al.expr()) args.Add(Visit(e));
                                i++; // skip argList
                            }
                            i++; // skip LPAREN
                            i++; // skip RPAREN
                        }
                        node = CreateNode("CallOtherNode", new Dictionary<string, object> { { "Target", node }, { "FuncName", funcName }, { "Arguments", args }, { "Args", args } });
                    } else if (term.Symbol.Type == LPCParser.LBRACKET) {
                        // 【創世補齊】完美處理索引訪問 a[b]！
                        var indexNode = Visit(context.GetChild(i + 1));
                        node = CreateNode("IndexAccessNode", new Dictionary<string, object> { { "Array", node }, { "Target", node }, { "Index", indexNode } });
                        i++; // skip expr
                        i++; // skip RBRACKET
                    }
                }
                i++;
            }
            return node;
        }'''
        
    code = code[:start_idx] + new_method + code[end_idx:]
    with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ VisitPostfixExpr 已完美重寫！LBRACKET 索引訪問路由已精準補齊！")
else:
    print("⚠ 找不到 VisitPostfixExpr！")
