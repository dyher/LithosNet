import re

with open("LithosNet.Compiler/Preprocessor.cs", "r", encoding="utf-8") as f:
    content = f.read()

# 在 Process 方法中加入過濾 Header Guard 的邏輯
new_filter = """
            // 【核心修復】過濾 Header Guard (#ifndef, 無值的 #define, #endif)
            source = Regex.Replace(source, @"^\s*#ifndef\s+\w+\s*$", "", RegexOptions.Multiline);
            source = Regex.Replace(source, @"^\s*#define\s+\w+\s*$", "", RegexOptions.Multiline);
            source = Regex.Replace(source, @"^\s*#endif\s*(?://.*)?$", "", RegexOptions.Multiline);
            
            // 處理 #include
"""

if "過濾 Header Guard" not in content:
    content = content.replace("// 處理 #include", new_filter)
    with open("LithosNet.Compiler/Preprocessor.cs", "w", encoding="utf-8") as f:
        f.write(content)
    print("✅ Preprocessor.cs 已升級：完美過濾 #ifndef / #endif！")
else:
    print("ℹ️ 已存在過濾邏輯！")
