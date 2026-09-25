import re

with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    builder = f.read()

# 尋找 VisitPrimaryExpr 方法並重寫
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
    
    new_method = '''public override AstNode VisitPrimaryExpr(LPCParser.PrimaryExprContext context) {
            // 【終極路由】強制精確轉發給 Literal Visitor！
            if (context.mappingLiteral() != null) return Visit(context.mappingLiteral());
            if (context.arrayLiteral() != null) return Visit(context.arrayLiteral());
            return base.VisitPrimaryExpr(context);
        }'''
        
    builder = builder[:start_idx] + new_method + builder[end_idx:]
    with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
        f.write(builder)
    print("✅ VisitPrimaryExpr 已強制注入精確路由！Mapping/Array 將被 100% 攔截！")
else:
    print("⚠ 找不到 VisitPrimaryExpr，嘗試直接注入...")
    injection = """
        public override AstNode VisitPrimaryExpr(LPCParser.PrimaryExprContext context) {
            if (context.mappingLiteral() != null) return Visit(context.mappingLiteral());
            if (context.arrayLiteral() != null) return Visit(context.arrayLiteral());
            return base.VisitPrimaryExpr(context);
        }
"""
    last_brace = builder.rfind('}')
    second_last_brace = builder.rfind('}', 0, last_brace)
    builder = builder[:second_last_brace] + injection + builder[second_last_brace:]
    with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
        f.write(builder)
    print("✅ VisitPrimaryExpr 已強制注入 (備用路徑)！")
