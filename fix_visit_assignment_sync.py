with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    code = f.read()

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
            // 【新語法】assignmentExpr : postfixExpr (ASSIGN|PLUS_ASSIGN|MINUS_ASSIGN) assignmentExpr | logicalOrExpr ;
            AstNode left = null;
            AstNode right = null;

            // 優先嘗試 postfixExpr 路徑 (索引賦值: map["key"] = val)
            if (context.postfixExpr() != null) {
                left = Visit(context.postfixExpr());
                if (context.assignmentExpr() != null) {
                    right = Visit(context.assignmentExpr());
                }
            }
            // 否則嘗試 logicalOrExpr 路徑 (一般賦值: x = val 或純表達式)
            else if (context.logicalOrExpr() != null) {
                left = Visit(context.logicalOrExpr());
                if (context.assignmentExpr() != null) {
                    right = Visit(context.assignmentExpr());
                }
            }

            if (left == null) return null;

            // 沒有賦值運算子 → 純表達式，直接返回
            if (right == null) return left;

            Console.WriteLine($"🔍 [Assign AST X-Ray] left Type: {left.GetType().Name}, Text: {context.GetText()}");

            // 【終極路由】如果左邊是 IndexAccessNode，生成 IndexAssignmentNode！
            if (left.GetType().Name == "IndexAccessNode") {
                var arrProp = left.GetType().GetProperty("Array") ?? left.GetType().GetProperty("Target");
                var idxProp = left.GetType().GetProperty("Index");
                var arrVal = arrProp?.GetValue(left);
                var idxVal = idxProp?.GetValue(left);
                Console.WriteLine($"🔍 [Assign AST X-Ray] IndexAssignment! Array={arrVal?.GetType().Name}, Index={idxVal?.GetType().Name}");
                return CreateNode("IndexAssignmentNode", new Dictionary<string, object> {
                    { "Array", arrVal },
                    { "Index", idxVal },
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
    print("✅ VisitAssignmentExpr 已完美同步！支援 postfixExpr 與 logicalOrExpr 雙路徑！")
else:
    print("⚠ 找不到 VisitAssignmentExpr！")
