using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LithosNet.Compiler {
    public class LpcPreprocessor {
        private HashSet<string> _includedFiles = new HashSet<string>();
        private Dictionary<string, string> _defines = new Dictionary<string, string>();
        private string _basePath;

        public LpcPreprocessor(string basePath) {
            _basePath = basePath;
        }

        public string Process(string filePath) {
            // 處理相對路徑與絕對路徑
            string fullPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(_basePath, filePath);
            
            // 嘗試尋找 include 目錄下的檔案
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
                
                // 1. 處理 #include "file.h" 或 <file.h>
                var includeMatch = Regex.Match(trimmed, @"^#include\s+[""<](.*)["">]");
                if (includeMatch.Success) {
                    string incFile = includeMatch.Groups[1].Value;
                    processedCode += Process(incFile) + "\n";
                    continue;
                }

                // 2. 處理 #define MACRO value
                var defineMatch = Regex.Match(trimmed, @"^#define\s+([A-Za-z0-9_]+)\s*(.*)");
                if (defineMatch.Success) {
                    _defines[defineMatch.Groups[1].Value] = defineMatch.Groups[2].Value.Trim();
                    continue;
                }

                // 3. 替換巨集 (簡單的單詞替換)
                string processedLine = line;
                foreach (var kvp in _defines) {
                    processedLine = Regex.Replace(processedLine, $@"\b{kvp.Key}\b", kvp.Value);
                }
                processedCode += processedLine + "\n";
            }
            return processedCode;
        }
    }
}
