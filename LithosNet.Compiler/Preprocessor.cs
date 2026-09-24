#nullable disable
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
