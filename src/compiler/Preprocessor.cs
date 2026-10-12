
using System;
using System.Collections.Generic;
using System.IO;

namespace LithosNet.Compiler;

// 1:1 from lib/lpc/preprocess.c + preprocess.h + lex.c include handling

public sealed class Preprocessor
{
    private readonly string _mudlibRoot;
    private readonly List<string> _includeDirs;
    private readonly Dictionary<string, string> _defines = new(StringComparer.Ordinal);

    public Preprocessor(string mudlibRoot, List<string> includeDirs = null)
    {
        _mudlibRoot = mudlibRoot ?? Directory.GetCurrentDirectory();
        _includeDirs = Lexer.NormalizeIncludeList(includeDirs ?? new List<string>(), _mudlibRoot);
    }

    public void Define(string name, string value)
    {
        if (!string.IsNullOrEmpty(name))
            _defines[name] = value ?? "";
    }

    public bool IsDefined(string name) => _defines.ContainsKey(name);

    // is_path_within_root from lex.c - sandbox containment
    public static bool IsPathWithinRoot(string root, string path)
    {
        return Lexer.IsPathWithinRoot(root, path);
    }

    // inc_open logic from lex.c
    public string ResolveInclude(string includeName, string currentFileDir)
    {
        // 1. relative to current file
        string candidate = Path.Combine(currentFileDir ?? _mudlibRoot, includeName);
        if (File.Exists(candidate) && IsPathWithinRoot(_mudlibRoot, candidate))
            return candidate;

        // 2. search include dirs
        foreach (var dir in _includeDirs)
        {
            string baseDir = dir;
            if (!Path.IsPathRooted(baseDir))
                baseDir = Path.Combine(_mudlibRoot, baseDir);

            // leading / means from mudlib root - already normalized
            candidate = Path.Combine(baseDir, includeName);
            if (File.Exists(candidate) && IsPathWithinRoot(_mudlibRoot, candidate))
                return candidate;
        }

        // 3. mudlib root fallback
        candidate = Path.Combine(_mudlibRoot, includeName);
        if (File.Exists(candidate) && IsPathWithinRoot(_mudlibRoot, candidate))
            return candidate;

        return null;
    }

    public string PreprocessFile(string filePath, out List<string> includedFiles)
    {
        includedFiles = new List<string>();
        if (!File.Exists(filePath)) return null;

        string content = File.ReadAllText(filePath);
        string dir = Path.GetDirectoryName(filePath) ?? _mudlibRoot;
        return PreprocessString(content, dir, includedFiles, 0);
    }

    private string PreprocessString(string content, string currentDir, List<string> includedFiles, int depth)
    {
        if (depth > 32) throw new InvalidOperationException("Include depth too deep");
        var lines = content.Split('\n');
        var sb = new System.Text.StringBuilder();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.TrimStart();

            if (trimmed.StartsWith("#include"))
            {
                // #include "file" or #include <file>
                int firstQuote = trimmed.IndexOf('"');
                int lastQuote = trimmed.LastIndexOf('"');
                string incName = null;
                if (firstQuote >= 0 && lastQuote > firstQuote)
                {
                    incName = trimmed.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                }
                else
                {
                    int lt = trimmed.IndexOf('<');
                    int gt = trimmed.IndexOf('>');
                    if (lt >= 0 && gt > lt)
                        incName = trimmed.Substring(lt + 1, gt - lt - 1);
                }

                if (incName != null)
                {
                    string resolved = ResolveInclude(incName, currentDir);
                    if (resolved != null)
                    {
                        includedFiles.Add(resolved);
                        string incContent = File.ReadAllText(resolved);
                        string incDir = Path.GetDirectoryName(resolved) ?? currentDir;
                        string processed = PreprocessString(incContent, incDir, includedFiles, depth + 1);
                        sb.AppendLine($"// begin include {incName} -> {resolved}");
                        sb.AppendLine(processed);
                        sb.AppendLine($"// end include {incName}");
                    }
                    else
                    {
                        sb.AppendLine($"// FAILED include {incName} not found");
                    }
                }
                else
                {
                    sb.AppendLine(line);
                }
            }
            else if (trimmed.StartsWith("#define"))
            {
                var parts = trimmed.Substring(7).Trim().Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 1)
                {
                    string name = parts[0];
                    string val = parts.Length > 1 ? parts[1] : "";
                    Define(name, val);
                }
                // keep line for line number preservation
                sb.AppendLine($"// {line}");
            }
            else if (trimmed.StartsWith("#pragma"))
            {
                // save_binary pragma handled by compiler
                sb.AppendLine(line);
            }
            else
            {
                // simple macro expansion for defined constants (non-recursive, per neolith)
                string expanded = line;
                foreach (var kv in _defines)
                {
                    // naive replace - real neolith does token-based
                    if (expanded.Contains(kv.Key))
                        expanded = expanded.Replace(kv.Key, kv.Value);
                }
                sb.AppendLine(expanded);
            }
        }

        return sb.ToString();
    }
}
