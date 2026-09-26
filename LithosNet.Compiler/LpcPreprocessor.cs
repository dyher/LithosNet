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
        private string _mudlibRoot;
        private Stack<string> _currentFilePaths = new Stack<string>();

        public LpcPreprocessor(string mudlibRoot) {
            _mudlibRoot = mudlibRoot ?? "";
        }

        public string Process(string filePath) {
            string fullPath = "";
            
            // 1. 如果是絕對路徑，直接使用
            if (Path.IsPathRooted(filePath)) {
                fullPath = filePath;
            } 
            // 2. 否則，相對於當前正在處理的檔案目錄尋找 (路徑記憶！)
            else if (_currentFilePaths.Count > 0) {
                string currentDir = Path.GetDirectoryName(_currentFilePaths.Peek());
                fullPath = Path.Combine(currentDir ?? "", filePath);
            } 
            // 3. 最後嘗試相對於 mudlibRoot
            else {
                fullPath = Path.Combine(_mudlibRoot, filePath);
            }

            fullPath = Path.GetFullPath(fullPath);

            if (!File.Exists(fullPath)) {
                // Fallback: 嘗試在 mudlibRoot/include/ 下尋找 (全域 Include 目錄)
                string incPath = Path.Combine(_mudlibRoot, "include", Path.GetFileName(filePath));
                if (File.Exists(incPath)) {
                    fullPath = Path.GetFullPath(incPath);
                } else {
                    // 最後嘗試直接相對於 mudlibRoot
                    string rootPath = Path.Combine(_mudlibRoot, filePath);
                    if (File.Exists(rootPath)) {
                        fullPath = Path.GetFullPath(rootPath);
                    } else {
                        Console.WriteLine($"⚠ [Preprocessor] File not found: {filePath}");
                        return $"// File not found: {filePath}\n";
                    }
                }
            }

            if (_includedFiles.Contains(fullPath)) return $"// Already included: {Path.GetFileName(fullPath)}\n";
            _includedFiles.Add(fullPath);
            _currentFilePaths.Push(fullPath); // Push 當前路徑，支援遞迴 include

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

                var macroMatch = Regex.Match(trimmed, @"^#define\s+([A-Za-z0-9_]+)\(([^)]*)\)\s*(.*)");
                if (macroMatch.Success) {
                    string name = macroMatch.Groups[1].Value;
                    var args = macroMatch.Groups[2].Value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                    string body = macroMatch.Groups[3].Value.Trim();
                    _macroFunctions[name] = (args, body);
                    continue;
                }

                var defineMatch = Regex.Match(trimmed, @"^#define\s+([A-Za-z0-9_]+)\s*(.*)");
                if (defineMatch.Success) {
                    _defines[defineMatch.Groups[1].Value] = defineMatch.Groups[2].Value.Trim();
                    continue;
                }

                string processedLine = line;
                
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

                foreach (var kvp in _defines) {
                    processedLine = Regex.Replace(processedLine, $@"\b{kvp.Key}\b", kvp.Value);
                }
                processedCode += processedLine + "\n";
            }
            
            _currentFilePaths.Pop(); // Pop 當前路徑
            return processedCode;
        }

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
