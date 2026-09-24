import re

with open('LithosNet.Core/LpcValue.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# 精準替換 AsInt (移除 throw，支援 String 轉 Int)
new_asint = '''public int AsInt() {
            if (Type == LpcType.Int) return (int)_primitiveValue;
            if (Type == LpcType.String && _referenceValue != null && int.TryParse(_referenceValue.ToString(), out int res)) return res;
            return 0; // 寬容 Fallback
        }'''
code = re.sub(r'public int AsInt\(\) => [^;]+;', new_asint, code)

# 精準替換 AsString (移除 throw，支援 Int/Object 轉 String)
new_asstring = '''public string AsString() {
            if (Type == LpcType.String) return _referenceValue?.ToString() ?? "";
            if (Type == LpcType.Int) return _primitiveValue.ToString();
            if (Type == LpcType.Object || Type == LpcType.Array || Type == LpcType.Mapping) return _referenceValue?.ToString() ?? "";
            return ""; // 寬容 Fallback
        }'''
code = re.sub(r'public string AsString\(\) => [^;]+;', new_asstring, code)

with open('LithosNet.Core/LpcValue.cs', 'w', encoding='utf-8') as f:
    f.write(code)
print("✅ LpcValue.cs 已賦予「寬容轉換」能力！徹底移除 throw InvalidCastException！")
