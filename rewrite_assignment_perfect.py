import re

with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# 精準鎖定並重寫整個 VisitAssignmentExpr 方法
start_idx = code.find('public override AstNode VisitAssignmentExpr')
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

    new_method = '''public override AstNode VisitAssignmentExpr(LPCParser.AssignmentExprContext context) {
            if (context.assignmentExpr() != null) {
                var left = Visit(context.logicalOrExpr());
                var right = Visit(context.assignmentExpr());
                
                // 【終極路由】如果左邊是索引存取 (IndexAccessNode)，強制生成 IndexAssignmentNode！
                if (left != null && left.GetType().Name == "IndexAccessNode") {
                    var arrProp = left.GetType().GetProperty("Array") ?? left.GetType().GetProperty("Target");
                    var idxProp = left.GetType().GetProperty("Index");
                    return CreateNode("IndexAssignmentNode", new Dictionary<string, object> {
                        { "Array", arrProp?.GetValue(left) },
                        { "Index", idxProp?.GetValue(left) },
                        { "Value", right }
                    });
                }

                string varName = "unknown";
                if (left != null) {
                    var nameProp = left.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var nameField = left.GetType().GetField("Name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (nameProp != null) varName = nameProp.GetValue(left)?.ToString() ?? "unknown";
                    else if (nameField != null) varName = nameField.GetValue(left)?.ToString() ?? "unknown";
                }
                
                return CreateNode("AssignmentNode", new Dictionary<string, object> { { "VariableName", varName }, { "Value", right } });
            }
            return Visit(context.logicalOrExpr());
        }'''
        
    code = code[:start_idx] + new_method + code[end_idx:]
    with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ VisitAssignmentExpr 已完美重寫！IndexAssignmentNode 路由已精準補齊！")
else:
    print("⚠ 找不到 VisitAssignmentExpr！")
