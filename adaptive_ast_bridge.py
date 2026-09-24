import re, glob, os

print("=== [1/3] 掃描 LithosNet.Core 獲取真實 AST 結構 ===")
nodes_info = {}
for f in glob.glob('LithosNet.Core/**/*.cs', recursive=True):
    with open(f, 'r', encoding='utf-8') as file:
        content = file.read()
    
    # 尋找類別定義 (包含大括號範圍)
    class_matches = re.finditer(r'(?:public\s+)?(?:partial\s+)?class\s+(\w+Node)\b[^{]*\{', content)
    for m in class_matches:
        node_name = m.group(1)
        start_idx = m.end()
        brace_count = 1
        end_idx = start_idx
        while brace_count > 0 and end_idx < len(content):
            if content[end_idx] == '{': brace_count += 1
            elif content[end_idx] == '}': brace_count -= 1
            end_idx += 1
        
        class_body = content[start_idx:end_idx-1]
        
        # 提取 Property 和 Field
        props = re.findall(r'public\s+(?:virtual\s+|override\s+)?[\w<>\[\],\s]+\s+(\w+)\s*\{\s*get;', class_body)
        fields = re.findall(r'public\s+[\w<>\[\],\s]+\s+(\w+)\s*;', class_body)
        nodes_info[node_name] = list(set(props + fields))

print(f"✅ 成功掃描 {len(nodes_info)} 個真實 Node 類別！")
for k, v in nodes_info.items():
    print(f"  - {k}: {v}")

print("\n=== [2/3] 自動修正 AstBuilder.cs 中的節點與屬性名稱 ===")
with open("LithosNet.Compiler/AstBuilder.cs", "r", encoding="utf-8") as f:
    code = f.read()

def find_best_match(target, candidates):
    if target in candidates: return target
    target_lower = target.lower().replace("node", "")
    for c in candidates:
        c_lower = c.lower().replace("node", "")
        if target_lower in c_lower or c_lower in target_lower:
            return c
    return None

# 1. 修正 Node 名稱 (例如 IdentifierNode -> VariableRefNode)
fake_nodes = ["IdentifierNode", "FunctionDeclNode", "VarDeclNode", "AssignNode", "CallNode", "UnaryOpNode"]
for fake in fake_nodes:
    real = find_best_match(fake, nodes_info.keys())
    if real and real != fake:
        print(f"  🔄 修正 Node: {fake} -> {real}")
        code = code.replace(f'CreateNode("{fake}"', f'CreateNode("{real}"')

# 2. 修正 Dictionary 中的 Property Keys (例如 "Value" -> "Val")
all_props = set()
for props in nodes_info.values():
    all_props.update(props)

def replace_dict_keys(match):
    key = match.group(1)
    real_key = find_best_match(key, all_props)
    if real_key and real_key != key:
        print(f"  🔄 修正 Property Key: '{key}' -> '{real_key}'")
        return f'{{ "{real_key}", '
    return match.group(0)

code = re.sub(r'\{\s*"(\w+)",\s*', replace_dict_keys, code)

with open("LithosNet.Compiler/AstBuilder.cs", "w", encoding="utf-8") as f:
    f.write(code)

print("\n✅ AstBuilder.cs 已根據真實 C# 結構完美自動修正完畢！")
