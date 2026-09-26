import re

# 1. 恢復 LPC.g4 的正統 Grammar
with open('./LithosNet.Compiler/Grammar/LPC.g4', 'r', encoding='utf-8') as f:
    g4 = f.read()

g4 = re.sub(
    r'assignmentExpr\s*:\s*[^;]+;',
    'assignmentExpr\n    : logicalOrExpr ((ASSIGN | PLUS_ASSIGN | MINUS_ASSIGN) assignmentExpr)?\n    ;',
    g4
)
with open('./LithosNet.Compiler/Grammar/LPC.g4', 'w', encoding='utf-8') as f:
    f.write(g4)
print("✅ LPC.g4 已恢復正統右遞迴結構！")

# 2. 完美重寫 VisitAssignmentExpr
with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    code = f.read()

start_idx = code.find('public override AstNode VisitAssignmentExpr')
if start_idx != -1:
    brace_count = 0
    in_method = False
    end_idx = start_idx
    for i in range(start_idx, len(code)):
        if code[i] == '{': brace_count += 1; in_method = True
        elif code[i] == '}':
            brace_count -= 1
            if in_method and brace_count == 0:
                end_idx = i + 1
                break

    # 【終極完美版】只呼叫 logicalOrExpr，並精準路由 IndexAccessNode！
    new_method = '''public override AstNode VisitAssignmentExpr(LPCParser.AssignmentExprContext context) {
            AstNode left = Visit(context.logicalOrExpr());
            if (left == null) return null;

            // 如果沒有賦值運算子，它就是一個純表達式，直接返回
            if (context.assignmentExpr() == null) return left;

            AstNode right = Visit(context.assignmentExpr());
            Console.WriteLine($"🔍 [Assign AST X-Ray] left Type: {left.GetType().Name}, Text: {context.GetText()}");

            // 【終極路由】如果左邊是 IndexAccessNode，生成 IndexAssignmentNode！
            if (left.GetType().Name == "IndexAccessNode") {
                var arrProp = left.GetType().GetProperty("Array") ?? left.GetType().GetProperty("Target");
                var idxProp = left.GetType().GetProperty("Index");
                return CreateNode("IndexAssignmentNode", new Dictionary<string, object> {
                    { "Array", arrProp?.GetValue(left) },
                    { "Index", idxProp?.GetValue(left) },
                    { "Value", right }
                });
            }

            // 一般變數賦值
            string varName = "unknown";
            var nameProp = left.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var nameField = left.GetType().GetField("Name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (nameProp != null) varName = nameProp.GetValue(left)?.ToString() ?? "unknown";
            else if (nameField != null) varName = nameField.GetValue(left)?.ToString() ?? "unknown";

            return CreateNode("AssignmentNode", new Dictionary<string, object> { { "VariableName", varName }, { "Value", right } });
        }'''
        
    code = code[:start_idx] + new_method + code[end_idx:]
    with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ VisitAssignmentExpr 已完美重寫！精準接收 logicalOrExpr 並路由 IndexAccessNode！")
