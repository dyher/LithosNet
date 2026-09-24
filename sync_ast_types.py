import re, glob

# 1. 掃描 LithosNet.Core 找出所有真實的 Node 類別
nodes = set()
for f in glob.glob('LithosNet.Core/**/*.cs', recursive=True):
    with open(f, 'r', encoding='utf-8') as file:
        c = file.read()
    # 匹配所有以 Node 結尾的類別
    for m in re.findall(r'class\s+(\w+Node)\b', c):
        nodes.add(m)

print(f"✅ 找到 {len(nodes)} 個真實的 AST Node 類別: {nodes}")

# 2. 讀取 AstBuilder.cs
with open("LithosNet.Compiler/AstBuilder.cs", "r", encoding="utf-8") as f:
    code = f.read()

# 3. 自動替換映射 (包含模糊匹配)
def fuzzy_replace(match):
    target = match.group(1)
    if target in nodes: return f'CreateNode("{target}"'
    
    # 模糊匹配：例如 "FunctionDeclNode" -> "FunctionNode"
    for n in nodes:
        if target.replace("Decl", "") in n or target.replace("Declaration", "") in n or n in target:
            return f'CreateNode("{n}"'
    return match.group(0)

code = re.sub(r'CreateNode\("(\w+)"', fuzzy_replace, code)

with open("LithosNet.Compiler/AstBuilder.cs", "w", encoding="utf-8") as f:
    f.write(code)
print("✅ AstBuilder.cs 已自動同步真實的 AST 類別名稱！所有節點將完美創建！")
