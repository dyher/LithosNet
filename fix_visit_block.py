import re

with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# 尋找 VisitBlock 方法並重寫它的遍歷邏輯
# 我們使用正則表達式精準替換 VisitBlock 的內部實作
old_pattern = r'public override AstNode VisitBlock\([^)]*\)\s*\{[^}]*\}'

new_block_logic = '''public override AstNode VisitBlock([System.Text.RegularExpressions.Regex]::Match(code, r'VisitBlock\((.*?)\)').Group(1)) {
            var block = new BlockNode();
            // 【終極修復】強制遍歷 context 中所有的 statement！
            if (context.statement() != null) {
                foreach (var stmtCtx in context.statement()) {
                    var astNode = Visit(stmtCtx);
                    if (astNode != null) block.Statements.Add(astNode);
                }
            }
            return block;
        }'''

# 為了避免正則替換失敗，我們使用更穩妥的字串替換策略
# 先嘗試找到 VisitBlock 的起始位置，然後手動計算大括號替換
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
    
    # 提取原始的參數列表 (例如 LPCParser.BlockContext context)
    param_match = re.search(r'VisitBlock\((.*?)\)', code[start_idx:end_idx])
    param_str = param_match.group(1) if param_match else "LPCParser.BlockContext context"
    
    new_method = f'''public override AstNode VisitBlock({param_str}) {{
            var block = new BlockNode();
            // 【終極修復】強制遍歷 context 中所有的 statement！
            if (context.statement() != null) {{
                foreach (var stmtCtx in context.statement()) {{
                    var astNode = Visit(stmtCtx);
                    if (astNode != null) block.Statements.Add(astNode);
                }}
            }}
            return block;
        }}'''
        
    code = code[:start_idx] + new_method + code[end_idx:]
    with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ VisitBlock 已完美重寫！強制遍歷所有 statement，徹底消滅語句丟棄！")
else:
    print("⚠️ 找不到 VisitBlock 方法！")
