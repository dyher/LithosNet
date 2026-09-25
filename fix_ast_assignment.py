import re

with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    builder = f.read()

# 尋找生成 AssignmentNode 的地方，通常長這樣：
# return CreateNode("AssignmentNode", new Dictionary... { {"VariableName", left...}, {"Value", right} });
# 我們需要把它替換為：如果 left 是 IndexAccessNode，則生成 IndexAssignmentNode

old_pattern = r'return CreateNode\("AssignmentNode", new Dictionary<string, object>\s*\{\s*\{\s*"VariableName",\s*([^\}]+)\s*\},\s*\{\s*"Value",\s*([^\}]+)\s*\}\s*\}\);'

# 為了安全起見，我們用更寬鬆的替換策略，直接攔截 "AssignmentNode" 的創建
if 'IndexAssignmentNode' not in builder or builder.count('IndexAssignmentNode') < 2:
    # 尋找包含 "AssignmentNode" 和 "VariableName" 的區塊
    injection = '''
            // 【終極路由】如果左邊是索引存取 (IndexAccessNode)，強制生成 IndexAssignmentNode！
            if (leftNode != null && leftNode.GetType().Name == "IndexAccessNode") {
                return CreateNode("IndexAssignmentNode", new Dictionary<string, object> {
                    { "Array", leftNode.GetType().GetProperty("Array")?.GetValue(leftNode) ?? leftNode.GetType().GetField("Array")?.GetValue(leftNode) },
                    { "Index", leftNode.GetType().GetProperty("Index")?.GetValue(leftNode) ?? leftNode.GetType().GetField("Index")?.GetValue(leftNode) },
                    { "Value", rightNode }
                });
            }
'''
    # 嘗試在生成 AssignmentNode 的前方注入
    builder = re.sub(
        r'(var rightNode = Visit\([^\)]+\);)',
        r'\1\n' + injection,
        builder, count=1
    )
    
    # 備用暴力替換 (如果上面沒匹配到)
    if 'IndexAssignmentNode' not in builder:
        builder = builder.replace(
            'CreateNode("AssignmentNode"',
            '(leftNode != null && leftNode.GetType().Name == "IndexAccessNode") ? CreateNode("IndexAssignmentNode", new Dictionary<string, object> { { "Array", leftNode.GetType().GetProperty("Array")?.GetValue(leftNode) }, { "Index", leftNode.GetType().GetProperty("Index")?.GetValue(leftNode) }, { "Value", rightNode } }) : CreateNode("AssignmentNode"'
        )

    with open('LithosNet.Compiler/AstBuilder.cs', 'w', encoding='utf-8') as f:
        f.write(builder)
    print("✅ AstBuilder.cs 已強制補齊 IndexAssignmentNode 路由！")
else:
    print("ℹ 路由已存在。")
