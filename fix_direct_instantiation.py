with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# 修復 1: VisitPostfixExpr 中的 IndexAccessNode 創建
old_index_create = '''node = CreateNode("IndexAccessNode", new Dictionary<string, object> { { "Array", node }, { "Target", node }, { "Index", indexNode } });
                        Console.WriteLine($"🔍 [Postfix LBRACKET X-Ray] CreateNode returned: {(node != null ? node.GetType().Name : "NULL")}");'''

new_index_create = '''var indexAccessNode = new IndexAccessNode();
                        indexAccessNode.Array = node;
                        indexAccessNode.Index = indexNode;
                        node = indexAccessNode;
                        Console.WriteLine($"🔍 [Postfix LBRACKET X-Ray] Direct new IndexAccessNode created! Array={node.GetType().Name}");'''

code = code.replace(old_index_create, new_index_create)

# 修復 2: VisitAssignmentExpr 中的 IndexAssignmentNode 創建
old_assign_create = '''if (left.GetType().Name == "IndexAccessNode") {
                var arrProp = left.GetType().GetProperty("Array") ?? left.GetType().GetProperty("Target");
                var idxProp = left.GetType().GetProperty("Index");
                return CreateNode("IndexAssignmentNode", new Dictionary<string, object> {
                    { "Array", arrProp?.GetValue(left) },
                    { "Index", idxProp?.GetValue(left) },
                    { "Value", right }
                });
            }'''

new_assign_create = '''if (left is IndexAccessNode ian) {
                Console.WriteLine($"🔍 [Assign AST X-Ray] IndexAssignment! Array={ian.Array?.GetType().Name}, Index={ian.Index?.GetType().Name}");
                var idxAssignNode = new IndexAssignmentNode();
                idxAssignNode.Array = ian.Array;
                idxAssignNode.Index = ian.Index;
                idxAssignNode.Value = right;
                return idxAssignNode;
            }'''

code = code.replace(old_assign_create, new_assign_create)

with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
    f.write(code)
print("✅ 已繞過 CreateNode 反射，直接實例化 IndexAccessNode 與 IndexAssignmentNode！")
