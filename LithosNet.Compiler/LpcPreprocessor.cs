using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LithosNet.Compiler {
    public class LpcPreprocessor {
        private HashSet<string> _includedFiles = new HashSet<string>();
        private Dictionary<string, string> _defines = new Dictionary<string, string>();
        private Dictionary<string, (List<string> Args, string Body)> _macroFunctions = new Dictionary<string, (List<string>, string)>();
        private string _basePath;

        public LpcPreprocessor(string basePath) {
            _basePath = basePath;
        }

        public string Process(string filePath) {
            string fullPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(_basePath, filePath);
            
            if (!File.Exists(fullPath)) {
                string incPath = Path.Combine(_basePath, "include", Path.GetFileName(filePath));
                if (File.Exists(incPath)) fullPath = incPath;
                else {
                    Console.WriteLine($"⚠ [Preprocessor] File not found: {fullPath}");
                    return $"// File not found: {filePath}\n";
                }
            }

            if (_includedFiles.Contains(fullPath)) return $"// Already included: {filePath}\n";
            _includedFiles.Add(fullPath);

            string code = File.ReadAllText(fullPath);
            string processedCode = $"// === Included: {Path.GetFileName(fullPath)} ===\n";

            foreach (var rawLine in code.Split('\n')) {
                string line = rawLine.TrimEnd('\r');
                string trimmed = line.Trim();
                
                var includeMatch = Regex.Match(trimmed, @"^#include\s+[""<](.*)["">]");
                if (includeMatch.Success) {
                    string incFile = includeMatch.Groups[1].Value;
                    processedCode += Process(incFile) + "\n";
                    continue;
                }

                // 【創世升級】支援帶參數巨集 #define MACRO(a, b) body
                var macroMatch = Regex.Match(trimmed, @"^#define\s+([A-Za-z0-9_]+)\(([^)]*)\)\s*(.*)");
                if (macroMatch.Success) {
                    string name = macroMatch.Groups[1].Value;
                    var args = macroMatch.Groups[2].Value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                    string body = macroMatch.Groups[3].Value.Trim();
                    _macroFunctions[name] = (args, body);
                    continue;
                }

                // 無參數巨集 #define MACRO value
                var defineMatch = Regex.Match(trimmed, @"^#define\s+([A-Za-z0-9_]+)\s*(.*)");
                if (defineMatch.Success) {
                    _defines[defineMatch.Groups[1].Value] = defineMatch.Groups[2].Value.Trim();
                    continue;
                }

                string processedLine = line;
                
                // 1. 替換帶參數巨集 (包含括號深度平衡)
                foreach (var kvp in _macroFunctions) {
                    string pattern = $@"\b{kvp.Key}\s*\(";
                    Match m = Regex.Match(processedLine, pattern);
                    while (m.Success) {
                        int startIdx = m.Index + m.Length;
                        int depth = 1;
                        int endIdx = startIdx;
                        while (endIdx < processedLine.Length && depth > 0) {
                            if (processedLine[endIdx] == '(') depth++;
                            else if (processedLine[endIdx] == ')') depth--;
                            endIdx++;
                        }
                        if (depth == 0) {
                            string argsStr = processedLine.Substring(startIdx, endIdx - startIdx - 1);
                            var actualArgs = SplitArgs(argsStr);
                            string expansion = kvp.Value.Body;
                            for (int i = 0; i < kvp.Value.Args.Count && i < actualArgs.Count; i++) {
                                expansion = Regex.Replace(expansion, $@"\b{kvp.Value.Args[i]}\b", actualArgs[i]);
                            }
                            processedLine = processedLine.Substring(0, m.Index) + expansion + processedLine.Substring(endIdx);
                            m = Regex.Match(processedLine, pattern);
                        } else {
                            break;
                        }
                    }
                }

                // 2. 替換無參數巨集
                foreach (var kvp in _defines) {
                    processedLine = Regex.Replace(processedLine, $@"\b{kvp.Key}\b", kvp.Value);
                }
                processedCode += processedLine + "\n";
            }
            return processedCode;
        }

        // 輔助函數：精準分割巨集參數 (處理參數內部包含逗號的情況，如函數呼叫)
        private List<string> SplitArgs(string argsStr) {
            var result = new List<string>();
            int depth = 0;
            int start = 0;
            for (int i = 0; i < argsStr.Length; i++) {
                char c = argsStr[i];
                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}') depth--;
                else if (c == ',' && depth == 0) {
                    result.Add(argsStr.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }
            result.Add(argsStr.Substring(start).Trim());
            return result;
        }
    }
}
