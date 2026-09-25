import re

with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    builder = f.read()

# 我們直接無情地替換整個 VisitPrimaryExpr 方法，確保包含所有邏輯
start_idx = builder.find('public override AstNode VisitPrimaryExpr')
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
    
    # 【終極完美版】包含 Mapping/Array 路由，且保留 ID/INT/STRING/expr 處理！
    new_method = '''public override AstNode VisitPrimaryExpr(LPCParser.PrimaryExprContext context) {
            // 【最高優先級】Literal 路由
            if (context.mappingLiteral() != null) return Visit(context.mappingLiteral());
            if (context.arrayLiteral() != null) return Visit(context.arrayLiteral());
            
            // 【原有核心邏輯】變數與常數處理
            if (context.ID() != null) return CreateNode("VariableRefNode", new Dictionary<string, object> { { "Name", context.ID().GetText() } });
            if (context.INT_LITERAL() != null) return CreateNode("LiteralNode", new Dictionary<string, object> { { "Value", LpcValue.Create(int.Parse(context.INT_LITERAL().GetText())) } });
            if (context.STRING_LITERAL() != null) return CreateNode("LiteralNode", new Dictionary<string, object> { { "Value", LpcValue.Create(context.STRING_LITERAL().GetText().Trim('"')) } });
            
            // 【遞迴處理】括號表達式
            if (context.expr() != null) return Visit(context.expr());
            
            return null;
        }'''
        
    builder = builder[:start_idx] + new_method + builder[end_idx:]
    with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
        f.write(builder)
    print("✅ VisitPrimaryExpr 已完美融合新舊邏輯！AST 路由大崩壞已終結！")
else:
    print("⚠ 找不到 VisitPrimaryExpr！")
