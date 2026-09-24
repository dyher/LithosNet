import re

with open('LithosNet.Core/LpcValue.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# 1. 精準替換 AsString 方法主體
code = re.sub(
    r'public\s+string\s+AsString\s*\(\s*\)\s*\{[^}]*\}',
    r'public string AsString() { if (Value == null) return ""; if (Value is string s) return s; return Convert.ToString(Value) ?? ""; }',
    code, flags=re.DOTALL
)

# 2. 精準替換 AsInt 方法主體
code = re.sub(
    r'public\s+int\s+AsInt\s*\(\s*\)\s*\{[^}]*\}',
    r'public int AsInt() { if (Value == null) return 0; if (Value is int i) return i; return Convert.ToInt32(Value); }',
    code, flags=re.DOTALL
)

# 3. 精準替換 AsBool 方法主體 (防禦性修復)
code = re.sub(
    r'public\s+bool\s+AsBool\s*\(\s*\)\s*\{[^}]*\}',
    r'public bool AsBool() { if (Value == null) return false; if (Value is bool b) return b; return Convert.ToBoolean(Value); }',
    code, flags=re.DOTALL
)

# 確保頂部有 using System;
if "using System;" not in code:
    code = "using System;\n" + code

with open('LithosNet.Core/LpcValue.cs', 'w', encoding='utf-8') as f:
    f.write(code)
print("✅ LpcValue.cs 的 AsString/AsInt/AsBool 已被核彈級重寫！第 29 行的炸彈已徹底拆除！")
