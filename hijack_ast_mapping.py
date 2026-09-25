import re

with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    builder = f.read()

# 尋找 VisitArrayLiteral 方法並重寫
start_idx = builder.find('public override AstNode VisitArrayLiteral')
if start_idx != -1:
    brace_count = 0
    in_method = False
    end_idx = start_idx
    for i in range(start_idx, len(builder)):
        if builder[i] == '{':
            brace_count += 1
            in_method = True
        elif builder[i] == '}':
            brace_count -= 1
            if in_method and brace_count == 0:
                end_idx = i + 1
                break
    
    # 【終極劫持】檢查原始文本，如果是 ([ 開頭，強制生成 MappingLiteralNode！
    new_method = '''public override AstNode VisitArrayLiteral(LPCParser.ArrayLiteralContext context) {
            string rawText = context.GetText();
            Console.WriteLine($"🔍 [AST Hijack] ArrayLiteral Text: {rawText.Substring(0, Math.Min(20, rawText.Length))}");
            
            // 【型別劫持】如果以 ([ 開頭，強制當作 Mapping 處理！
            if (rawText.StartsWith("([") || rawText.StartsWith("([")) {
                var mapNode = new MappingLiteralNode();
                if (context.expr() != null) {
                    var exprs = context.expr();
                    for (int i = 0; i < exprs.Length; i += 2) {
                        var keyNode = Visit(exprs[i]);
                        var valNode = (i + 1 < exprs.Length) ? Visit(exprs[i + 1]) : null;
                        if (keyNode != null) mapNode.Keys.Add(keyNode);
                        if (valNode != null) mapNode.Values.Add(valNode);
                    }
                }
                Console.WriteLine($"🔍 [AST Hijack] Converted to MappingLiteralNode with {mapNode.Keys.Count} keys!");
                return mapNode;
            }

            // 原本的 Array 邏輯
            var node = new ArrayLiteralNode();
            if (context.expr() != null) {
                foreach (var e in context.expr()) {
                    var astNode = Visit(e);
                    if (astNode != null) node.Elements.Add(astNode);
                }
            }
            return node;
        }'''
        
    builder = builder[:start_idx] + new_method + builder[end_idx:]
    with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
        f.write(builder)
    print("✅ VisitArrayLiteral 已注入 AST 型別劫持！無視 ANTLR 規則，強制轉換 Mapping！")
else:
    print("⚠ 找不到 VisitArrayLiteral！")
