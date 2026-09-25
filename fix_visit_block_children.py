import re

with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# 尋找 VisitBlock 方法並用 children 遍歷重寫
start_idx = code.find('public override AstNode VisitBlock')
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
    
    param_match = re.search(r'VisitBlock\((.*?)\)', code[start_idx:end_idx])
    param_str = param_match.group(1) if param_match else "LPCParser.BlockContext context"
    
    new_method = f'''public override AstNode VisitBlock({param_str}) {{
            var block = new BlockNode();
            // 【終極降維】放棄 context.statement()，直接遍歷最底層的 context.children！
            if (context.children != null) {{
                foreach (var child in context.children) {{
                    if (child is LPCParser.StatementContext stmtCtx) {{
                        var astNode = Visit(stmtCtx);
                        Console.WriteLine($"🔍 [Block X-Ray] Child Type: {{child.GetType().Name}}, AST Node: {{(astNode != null ? astNode.GetType().Name : "NULL")}}");
                        if (astNode != null) block.Statements.Add(astNode);
                    }}
                }}
            }}
            return block;
        }}'''
        
    code = code[:start_idx] + new_method + code[end_idx:]
    with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ VisitBlock 已重寫為遍歷 context.children！徹底繞過 ANTLR 生成缺陷！")
else:
    print("⚠️ 找不到 VisitBlock 方法！")
