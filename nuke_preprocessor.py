# 使用 r"""...""" (Raw String) 確保 \s 和 \w 完美傳遞給 C#
code = r"""#nullable disable
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace LithosNet.Compiler {
    public static class Preprocessor {
        public static string Process(string source, string basePath) {
            // 1. 處理 #include "file.h" 或 #include <file.h>
            string includePattern = @"#include\s+[<""']([^>""']+)[>""']";
            source = Regex.Replace(source, includePattern, match => {
                string fileName = match.Groups[1].Value;
                string fullPath = Path.Combine(basePath, fileName);
                if (!File.Exists(fullPath)) {
                    fullPath = Path.Combine("/home/tiny/LithosNet/mudlib/include", fileName);
                }
                if (File.Exists(fullPath)) {
                    return File.ReadAllText(fullPath);
                }
                return $"/* INCLUDE NOT FOUND: {fileName} */";
            });

            // 2. 移除 Header Guards (#ifndef, 無值的 #define, #endif)
            source = Regex.Replace(source, @"^\s*#ifndef\s+\w+\s*$", "", RegexOptions.Multiline);
            source = Regex.Replace(source, @"^\s*#define\s+\w+\s*$", "", RegexOptions.Multiline);
            source = Regex.Replace(source, @"^\s*#endif\s*(?://.*)?$", "", RegexOptions.Multiline);

            // 3. 處理有值的 #define MACRO value
            string definePattern = @"^\s*#define\s+(\w+)\s+(.*)$";
            var matches = Regex.Matches(source, definePattern, RegexOptions.Multiline);
            foreach (Match m in matches) {
                string macro = m.Groups[1].Value;
                string value = m.Groups[2].Value.Trim();
                source = source.Replace(m.Value, ""); 
                source = Regex.Replace(source, @"\b" + macro + @"\b", value);
            }
            
            return source;
        }
    }
}
"""
with open("LithosNet.Compiler/Preprocessor.cs", "w", encoding="utf-8") as f:
    f.write(code)
print("✅ Preprocessor.cs 已核彈級重寫！徹底免疫 Python 轉義陷阱！")
