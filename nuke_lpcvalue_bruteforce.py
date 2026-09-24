import re

with open('LithosNet.Core/LpcValue.cs', 'r', encoding='utf-8') as f:
    code = f.read()

print("=== 🔍 原始檔案中包含強制轉換的行 (犯罪證據) ===")
for i, line in enumerate(code.split('\n')):
    if '(string)' in line or '(int)' in line or '(bool)' in line:
        print(f"Line {i+1}: {line.strip()}")

# 【核彈級替換】無視語法結構，直接替換關鍵字！
# 將 (string)Value 替換為 Convert.ToString(Value)
code = re.sub(r'\(string\)\s*([a-zA-Z_][a-zA-Z0-9_\.]*)', r'Convert.ToString(\1)', code)
code = re.sub(r'\(int\)\s*([a-zA-Z_][a-zA-Z0-9_\.]*)', r'Convert.ToInt32(\1)', code)
code = re.sub(r'\(bool\)\s*([a-zA-Z_][a-zA-Z0-9_\.]*)', r'Convert.ToBoolean(\1)', code)

# 確保頂部有 using System;
if 'using System;' not in code:
    code = 'using System;\n' + code

with open('LithosNet.Core/LpcValue.cs', 'w', encoding='utf-8') as f:
    f.write(code)

print("\n=== ✅ 替換後的 LpcValue.cs 真實內容 (前 40 行) ===")
for i, line in enumerate(code.split('\n')[:40]):
    print(f"{i+1:2}: {line}")
