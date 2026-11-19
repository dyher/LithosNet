using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace LithosNet.Compiler;

public sealed class Preprocessor
{
    private readonly string _mudlibRoot;
    private readonly List<string> _includeDirs;
    private readonly Dictionary<string, Macro> _macros = new(StringComparer.Ordinal);
    public string GlobalIncludePath { get; set; }

    private sealed class Macro
    {
        public string Name;
        public string Body;
        public bool IsFunc;
        public List<string> Params;
    }

    private sealed class CondState
    {
        public bool ParentActive;
        public bool Condition;
        public bool Active;
        public bool ElseSeen;
    }

    public Preprocessor(string root, List<string> inc = null)
    {
        _mudlibRoot = root ?? Directory.GetCurrentDirectory();
        _includeDirs = NormalizeInc(inc ?? new List<string>());
        // Neolith predefines (add_predefines): __LPC__ is a number, __DRIVER__/__VERSION__ are quoted strings.
        _macros["__LPC__"] = new Macro { Name = "__LPC__", Body = "1", IsFunc = false };
        _macros["__DRIVER__"] = new Macro { Name = "__DRIVER__", Body = "\"LithosNet\"", IsFunc = false };
        _macros["__VERSION__"] = new Macro { Name = "__VERSION__", Body = "\"V4\"", IsFunc = false };
    }

    // Neolith start_new_file: __FILE__ = "/path/to/file", __DIR__ = "/path/to/dir/"
    // (path relative to mudlib root, with leading '/'; quoted string literal).
    // Returns the previous __FILE__/__DIR__ so the caller can restore them
    // (an #include gets its own __FILE__/__DIR__, restored when it ends).
    private (Macro prevFile, Macro prevDir) SetFileMacros(string filePath)
    {
        _macros.TryGetValue("__FILE__", out var prevFile);
        _macros.TryGetValue("__DIR__", out var prevDir);
        if (string.IsNullOrEmpty(filePath)) return (prevFile, prevDir);
        string rel;
        try
        {
            string full = Path.GetFullPath(filePath);
            string root = Path.GetFullPath(_mudlibRoot);
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                rel = full.Substring(root.Length).Replace('\\', '/');
            else
                rel = "/" + Path.GetFileName(full);
        }
        catch { rel = "/" + Path.GetFileName(filePath); }
        if (!rel.StartsWith("/")) rel = "/" + rel;
        _macros["__FILE__"] = new Macro { Name = "__FILE__", Body = "\"" + rel + "\"", IsFunc = false };
        string dir = rel.Substring(0, rel.LastIndexOf('/') + 1);
        _macros["__DIR__"] = new Macro { Name = "__DIR__", Body = "\"" + dir + "\"", IsFunc = false };
        return (prevFile, prevDir);
    }

    private void RestoreFileMacros((Macro prevFile, Macro prevDir) prev)
    {
        if (prev.prevFile != null) _macros["__FILE__"] = prev.prevFile;
        else _macros.Remove("__FILE__");
        if (prev.prevDir != null) _macros["__DIR__"] = prev.prevDir;
        else _macros.Remove("__DIR__");
    }

    private List<string> NormalizeInc(List<string> dirs)
    {
        var list = new List<string>();
        foreach (var d in dirs)
        {
            if (string.IsNullOrWhiteSpace(d)) continue;
            string p = d;
            if (!Path.IsPathRooted(p)) p = Path.Combine(_mudlibRoot, p);
            list.Add(p);
        }
        string defaultInc = Path.Combine(_mudlibRoot, "include");
        if (!list.Contains(defaultInc)) list.Add(defaultInc);
        return list;
    }

    public static bool IsPathWithinRoot(string root, string path)
    {
        try
        {
            string fullRoot = Path.GetFullPath(root);
            string fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public string ResolveInclude(string includeName, string currentFileDir)
    {
        string inc = includeName;
        if (inc.StartsWith("/")) inc = inc.Substring(1);
        if (!string.IsNullOrEmpty(currentFileDir))
        {
            string cand = Path.Combine(currentFileDir, inc);
            if (File.Exists(cand)) return cand;
            cand = Path.Combine(currentFileDir, includeName);
            if (File.Exists(cand)) return cand;
        }
        foreach (var dir in _includeDirs)
        {
            string cand = Path.Combine(dir, inc);
            if (File.Exists(cand)) return cand;
            cand = Path.Combine(dir, includeName);
            if (File.Exists(cand)) return cand;
        }
        string cand2 = Path.Combine(_mudlibRoot, inc);
        if (File.Exists(cand2)) return cand2;
        cand2 = Path.Combine(_mudlibRoot, includeName);
        if (File.Exists(cand2)) return cand2;
        if (Path.IsPathRooted(includeName) && File.Exists(includeName)) return includeName;
        return null;
    }

    public string PreprocessFile(string filePath, out List<string> includedFiles)
    {
        includedFiles = new List<string>();
        if (!File.Exists(filePath)) return null;
        string dir = Path.GetDirectoryName(filePath) ?? _mudlibRoot;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        StringBuilder sb = new StringBuilder();
        if (!string.IsNullOrEmpty(GlobalIncludePath))
        {
            string gName = GlobalIncludePath.Trim();
            if (gName.StartsWith("<") && gName.EndsWith(">")) gName = gName.Substring(1, gName.Length - 2);
            else if (gName.StartsWith("\"") && gName.EndsWith("\"")) gName = gName.Substring(1, gName.Length - 2);
            string resolved = ResolveInclude(gName, dir);
            if (resolved != null && File.Exists(resolved))
            {
                includedFiles.Add(resolved);
                string content = File.ReadAllText(resolved);
                string processed = PreprocessString(content, Path.GetDirectoryName(resolved) ?? dir, resolved, includedFiles, visited, 0);
                sb.AppendLine(processed);
            }
        }
        string mainContent = File.ReadAllText(filePath);
        string mainProcessed = PreprocessString(mainContent, dir, filePath, includedFiles, visited, 0);
        sb.AppendLine(mainProcessed);
        return sb.ToString();
    }

    private string MergeContinuations(string content)
    {
        var lines = content.Split('\n');
        var sb = new StringBuilder();
        string buffer = "";
        bool inCont = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmedEnd = line.TrimEnd();
            if (trimmedEnd.EndsWith("\\"))
            {
                string without = trimmedEnd.Substring(0, trimmedEnd.Length - 1);
                if (inCont) buffer += without + " ";
                else buffer = without + " ";
                inCont = true;
            }
            else
            {
                if (inCont)
                {
                    buffer += line;
                    sb.AppendLine(buffer);
                    buffer = "";
                    inCont = false;
                }
                else
                {
                    sb.AppendLine(line);
                }
            }
        }
        if (inCont && buffer.Length > 0) sb.AppendLine(buffer);
        return sb.ToString();
    }

    private string PreprocessString(string content, string currentDir, string currentFile, List<string> includedFiles, HashSet<string> visited, int depth)
    {
        if (depth > 64) throw new InvalidOperationException("Include depth too deep");
        var savedFileMacros = SetFileMacros(currentFile);
        content = MergeContinuations(content);
        var lines = content.Split('\n');
        var sb = new StringBuilder();
        var condStack = new Stack<CondState>();

        bool IsActive()
        {
            if (condStack.Count == 0) return true;
            return condStack.Peek().Active;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            string rawLine = lines[i];
            string trimmed = rawLine.TrimStart();
            if (trimmed.StartsWith("#"))
            {
                string directiveLine = trimmed.Substring(1).TrimStart();
                if (directiveLine.StartsWith("include"))
                {
                    if (!IsActive()) continue;
                    string rest = directiveLine.Substring(7).Trim();
                    string incName = null;
                    int firstQuote = rest.IndexOf('"');
                    int lastQuote = rest.LastIndexOf('"');
                    if (firstQuote >= 0 && lastQuote > firstQuote)
                        incName = rest.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                    else
                    {
                        int lt = rest.IndexOf('<');
                        int gt = rest.IndexOf('>');
                        if (lt >= 0 && gt > lt)
                            incName = rest.Substring(lt + 1, gt - lt - 1);
                        else
                        {
                            var parts = rest.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 0) incName = parts[0];
                        }
                    }
                    if (incName != null)
                    {
                        string resolved = ResolveInclude(incName, currentDir);
                        if (resolved != null && File.Exists(resolved) && !visited.Contains(resolved))
                        {
                            visited.Add(resolved);
                            includedFiles.Add(resolved);
                            string incContent = File.ReadAllText(resolved);
                            string incDir = Path.GetDirectoryName(resolved) ?? currentDir;
                            string processed = PreprocessString(incContent, incDir, resolved, includedFiles, visited, depth + 1);
                            sb.AppendLine($"// begin include {incName} -> {resolved}");
                            sb.AppendLine(processed);
                            sb.AppendLine($"// end include {incName}");
                        }
                        else if (resolved == null)
                        {
                            sb.AppendLine($"// FAILED include {incName} not found (from {currentDir})");
                        }
                    }
                    continue;
                }
                else if (directiveLine.StartsWith("define"))
                {
                    if (!IsActive()) continue;
                    string rest = directiveLine.Substring(6).Trim();
                    if (string.IsNullOrEmpty(rest)) continue;
                    // Neolith handle_define: '(' must IMMEDIATELY follow the name for a function macro.
                    // "#define FOO (1+2)" is an object macro with body "(1+2)", not a function macro.
                    var m = Regex.Match(rest, @"^([A-Za-z_][A-Za-z0-9_]*)(\(([^)]*)\))?\s*(.*)$");
                    if (!m.Success) continue;
                    string name = m.Groups[1].Value;
                    string paramsPart = m.Groups[3].Value;
                    string body = m.Groups[4].Value ?? "";
                    body = StripBlockComments(body);
                    body = StripLineComment(body);
                    body = body.Trim();
                    bool isFunc = m.Groups[2].Success && !string.IsNullOrEmpty(m.Groups[2].Value);
                    List<string> paramList = null;
                    if (isFunc)
                    {
                        paramList = new List<string>();
                        if (!string.IsNullOrWhiteSpace(paramsPart))
                        {
                            foreach (var p in paramsPart.Split(','))
                            {
                                string pp = p.Trim();
                                if (!string.IsNullOrEmpty(pp)) paramList.Add(pp);
                            }
                        }
                    }
                    var macro = new Macro { Name = name, Body = body, IsFunc = isFunc, Params = paramList };
                    _macros[name] = macro;
                    continue;
                }
                else if (directiveLine.StartsWith("undef"))
                {
                    if (!IsActive()) continue;
                    string rest = directiveLine.Substring(5).Trim();
                    // 【entity-Neolith¦canonical_name=Neolith】: illegal to #undef a predefined value.
                    if (rest == "__FILE__" || rest == "__DIR__" || rest == "__LPC__" ||
                        rest == "__DRIVER__" || rest == "__VERSION__") continue;
                    _macros.Remove(rest);
                    continue;
                }
                else if (directiveLine.StartsWith("ifdef"))
                {
                    string rest = directiveLine.Substring(5).Trim();
                    bool defined = _macros.ContainsKey(rest);
                    bool parentActive = IsActive();
                    condStack.Push(new CondState { ParentActive = parentActive, Condition = defined, Active = parentActive && defined, ElseSeen = false });
                    continue;
                }
                else if (directiveLine.StartsWith("ifndef"))
                {
                    string rest = directiveLine.Substring(6).Trim();
                    bool defined = _macros.ContainsKey(rest);
                    bool parentActive = IsActive();
                    bool cond = !defined;
                    condStack.Push(new CondState { ParentActive = parentActive, Condition = cond, Active = parentActive && cond, ElseSeen = false });
                    continue;
                }
                else if (directiveLine.StartsWith("if"))
                {
                    string rest = directiveLine.Substring(2).Trim();
                    bool cond = EvaluateIf(rest);
                    bool parentActive = IsActive();
                    condStack.Push(new CondState { ParentActive = parentActive, Condition = cond, Active = parentActive && cond, ElseSeen = false });
                    continue;
                }
                else if (directiveLine.StartsWith("elif"))
                {
                    if (condStack.Count == 0) continue;
                    var top = condStack.Pop();
                    if (top.ElseSeen) { condStack.Push(top); continue; }
                    string rest = directiveLine.Substring(4).Trim();
                    bool cond = EvaluateIf(rest);
                    bool newActive = false;
                    if (!top.Condition && top.ParentActive) newActive = cond;
                    condStack.Push(new CondState { ParentActive = top.ParentActive, Condition = top.Condition || cond, Active = top.ParentActive && newActive && !top.Condition, ElseSeen = false });
                    continue;
                }
                else if (directiveLine.StartsWith("else"))
                {
                    if (condStack.Count == 0) continue;
                    var top = condStack.Pop();
                    if (top.ElseSeen) { condStack.Push(top); continue; }
                    bool newActive = top.ParentActive && !top.Condition;
                    condStack.Push(new CondState { ParentActive = top.ParentActive, Condition = true, Active = newActive, ElseSeen = true });
                    continue;
                }
                else if (directiveLine.StartsWith("endif"))
                {
                    if (condStack.Count > 0) condStack.Pop();
                    continue;
                }
                else if (directiveLine.StartsWith("pragma"))
                {
                    if (!IsActive()) continue;
                    sb.AppendLine(rawLine);
                    continue;
                }
                else
                {
                    if (IsActive()) sb.AppendLine(rawLine);
                    continue;
                }
            }
            else
            {
                if (!IsActive()) continue;
                string expanded = ExpandMacros(rawLine, _macros);
                sb.AppendLine(expanded);
            }
        }
        RestoreFileMacros(savedFileMacros);
        return sb.ToString();
    }

    private bool EvaluateIf(string expr)
    {
        if (string.IsNullOrWhiteSpace(expr)) return false;
        string e = expr.Trim();
        // defined(FOO) / defined FOO -> 1/0 (Neolith lex.c: defined expands in #if)
        e = Regex.Replace(e, @"defined\s*\(\s*([A-Za-z_][A-Za-z0-9_]*)\s*\)", m =>
            _macros.ContainsKey(m.Groups[1].Value) ? "1" : "0");
        e = Regex.Replace(e, @"defined\s+([A-Za-z_][A-Za-z0-9_]*)", m =>
            _macros.ContainsKey(m.Groups[1].Value) ? "1" : "0");
        // Expand object-like macros (bounded, like expand_define's EXPANDMAX).
        // String-valued macros (__FILE__/__DIR__/__DRIVER__) are skipped: they
        // become 0 below, matching Neolith (cond_get_exp errors on '"' -> 0).
        for (int iter = 0; iter < 16; iter++)
        {
            bool changed = false;
            foreach (var kv in _macros)
            {
                if (kv.Value.IsFunc) continue;
                if (string.IsNullOrEmpty(kv.Value.Body)) continue;
                if (kv.Value.Body.Contains('"')) continue;
                string newE = Regex.Replace(e, @"\b" + Regex.Escape(kv.Key) + @"\b", "(" + kv.Value.Body + ")");
                if (newE != e) { changed = true; e = newE; }
            }
            if (!changed) break;
        }
        // Neolith: identifiers that are not macros evaluate to 0 in #if.
        e = Regex.Replace(e, @"\b[A-Za-z_][A-Za-z0-9_]*\b", "0");
        try
        {
            var parser = new IfExprParser(e);
            return parser.Parse() != 0;
        }
        catch { return false; }
    }

    // #if expression evaluator, mirroring Neolith cond_get_exp precedence:
    // ?: (1) < || (2) < && (3) < | (4) < ^ (5) < & (6) < ==/!= (7)
    // < <=/>/>= (8) < << >> (9) < + - (10) < * / % (11) < unary ~ ! - + (12).
    // Numbers: decimal, octal (0 prefix), hex (0x prefix).
    private sealed class IfExprParser
    {
        private readonly List<string> _toks;
        private int _pos;
        public IfExprParser(string expr) { _toks = Tokenize(expr); _pos = 0; }

        private static List<string> Tokenize(string s)
        {
            var toks = new List<string>();
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }
                string two = i + 1 < s.Length ? s.Substring(i, 2) : "";
                if (two == "<<" || two == ">>" || two == "<=" || two == ">=" ||
                    two == "==" || two == "!=" || two == "&&" || two == "||")
                { toks.Add(two); i += 2; continue; }
                if ("()?:~!-+*/%<>&^|".IndexOf(c) >= 0) { toks.Add(c.ToString()); i++; continue; }
                if (char.IsDigit(c))
                {
                    int start = i;
                    if (c == '0' && i + 1 < s.Length && (s[i + 1] == 'x' || s[i + 1] == 'X'))
                    {
                        i += 2;
                        while (i < s.Length && Uri.IsHexDigit(s[i])) i++;
                    }
                    else
                    {
                        while (i < s.Length && char.IsDigit(s[i])) i++;
                    }
                    toks.Add(s.Substring(start, i - start));
                    continue;
                }
                throw new InvalidOperationException("illegal character in #if: " + c);
            }
            return toks;
        }

        private string Peek() => _pos < _toks.Count ? _toks[_pos] : null;
        private string Next() => _pos < _toks.Count ? _toks[_pos++] : null;
        private bool Eat(string op) { if (Peek() == op) { _pos++; return true; } return false; }

        public long Parse() { long v = ParseCond(); return v; }

        private long ParseCond()
        {
            long cond = ParseOr();
            if (Eat("?"))
            {
                long a = ParseCond();
                Eat(":");
                long b = ParseCond();
                return cond != 0 ? a : b;
            }
            return cond;
        }
        private long ParseOr() { long l = ParseAnd(); while (Eat("||")) { long r = ParseAnd(); l = (l != 0 || r != 0) ? 1 : 0; } return l; }
        private long ParseAnd() { long l = ParseBor(); while (Eat("&&")) { long r = ParseBor(); l = (l != 0 && r != 0) ? 1 : 0; } return l; }
        private long ParseBor() { long l = ParseXor(); while (Eat("|")) { long r = ParseXor(); l = l | r; } return l; }
        private long ParseXor() { long l = ParseBand(); while (Eat("^")) { long r = ParseBand(); l = l ^ r; } return l; }
        private long ParseBand() { long l = ParseEq(); while (Eat("&")) { long r = ParseEq(); l = l & r; } return l; }
        private long ParseEq()
        {
            long l = ParseRel();
            while (true)
            {
                if (Eat("==")) { long r = ParseRel(); l = l == r ? 1 : 0; }
                else if (Eat("!=")) { long r = ParseRel(); l = l != r ? 1 : 0; }
                else break;
            }
            return l;
        }
        private long ParseRel()
        {
            long l = ParseShift();
            while (true)
            {
                if (Eat("<=")) { long r = ParseShift(); l = l <= r ? 1 : 0; }
                else if (Eat(">=")) { long r = ParseShift(); l = l >= r ? 1 : 0; }
                else if (Eat("<")) { long r = ParseShift(); l = l < r ? 1 : 0; }
                else if (Eat(">")) { long r = ParseShift(); l = l > r ? 1 : 0; }
                else break;
            }
            return l;
        }
        private long ParseShift()
        {
            long l = ParseAdd();
            while (true)
            {
                if (Eat("<<")) { long r = ParseAdd(); l = l << (int)r; }
                else if (Eat(">>")) { long r = ParseAdd(); l = l >> (int)r; }
                else break;
            }
            return l;
        }
        private long ParseAdd()
        {
            long l = ParseMul();
            while (true)
            {
                if (Eat("+")) { long r = ParseMul(); l = l + r; }
                else if (Eat("-")) { long r = ParseMul(); l = l - r; }
                else break;
            }
            return l;
        }
        private long ParseMul()
        {
            long l = ParseUnary();
            while (true)
            {
                if (Eat("*")) { long r = ParseUnary(); l = l * r; }
                else if (Eat("/")) { long r = ParseUnary(); l = r != 0 ? l / r : 0; }
                else if (Eat("%")) { long r = ParseUnary(); l = r != 0 ? l % r : 0; }
                else break;
            }
            return l;
        }
        private long ParseUnary()
        {
            if (Eat("~")) return ~ParseUnary();
            if (Eat("!")) return ParseUnary() == 0 ? 1 : 0;
            if (Eat("-")) return -ParseUnary();
            if (Eat("+")) return ParseUnary();
            return ParsePrimary();
        }
        private long ParsePrimary()
        {
            if (Eat("(")) { long v = ParseCond(); Eat(")"); return v; }
            string t = Next();
            if (t == null) throw new InvalidOperationException("missing expression in #if");
            if (t.StartsWith("0x") || t.StartsWith("0X"))
            {
                long v = 0;
                foreach (char h in t.Substring(2))
                    v = v * 16 + (h <= '9' ? h - '0' : (h <= 'F' ? h - 'A' + 10 : h - 'a' + 10));
                return v;
            }
            if (t.Length > 1 && t[0] == '0')
            {
                long v = 0;
                foreach (char d in t) v = v * 8 + (d - '0');
                return v;
            }
            return long.Parse(t);
        }
    }

    private static string StripBlockComments(string body)
    {
        return Regex.Replace(body, @"/\*.*?\*/", "", RegexOptions.Singleline);
    }

    private static string StripLineComment(string body)
    {
        bool inStr = false;
        char strChar = '\0';
        for (int i = 0; i < body.Length - 1; i++)
        {
            char c = body[i];
            if (!inStr && (c == '"' || c == '\''))
            {
                inStr = true; strChar = c;
            }
            else if (inStr && c == strChar)
            {
                if (i > 0 && body[i - 1] == '\\') continue;
                inStr = false;
            }
            else if (!inStr && c == '/' && body[i + 1] == '/')
            {
                return body.Substring(0, i).TrimEnd();
            }
        }
        return body;
    }

    // 回傳 pos 是否落在字串字面值內（"..." 或 '...'）。用於 macro 展開時跳過字串內容，
    // 避免 write("MUD_NAME\n") 這類字串裡的 macro 名稱被誤展開。
    private static bool IsInsideString(string s, int pos)
    {
        bool inStr = false;
        char strChar = '\0';
        for (int i = 0; i < pos && i < s.Length; i++)
        {
            char ch = s[i];
            if (!inStr && (ch == '"' || ch == '\''))
            {
                inStr = true; strChar = ch;
            }
            else if (inStr && ch == strChar)
            {
                if (i > 0 && s[i - 1] == '\\') continue;
                inStr = false;
            }
        }
        return inStr;
    }

    private string ExpandMacros(string line, Dictionary<string, Macro> macros)
    {
        string codePart = line;
        string commentPart = "";
        bool inStr = false;
        char strChar = '\0';
        for (int i = 0; i < line.Length - 1; i++)
        {
            char c = line[i];
            if (!inStr && (c == '"' || c == '\''))
            {
                inStr = true; strChar = c;
            }
            else if (inStr && c == strChar)
            {
                if (i > 0 && line[i - 1] == '\\') continue;
                inStr = false;
            }
            else if (!inStr && c == '/' && line[i + 1] == '/')
            {
                codePart = line.Substring(0, i);
                commentPart = line.Substring(i);
                break;
            }
        }
        string work = codePart;
        for (int iterFunc = 0; iterFunc < 4; iterFunc++)
        {
            bool changedFunc = false;
            foreach (var kv in macros)
            {
                if (!kv.Value.IsFunc) continue;
                int idx = 0;
                while (true)
                {
                    int pos = work.IndexOf(kv.Key + "(", idx, StringComparison.Ordinal);
                    if (pos < 0) break;
                    if (pos > 0 && (char.IsLetterOrDigit(work[pos - 1]) || work[pos - 1] == '_'))
                    {
                        idx = pos + 1; continue;
                    }
                    if (IsInsideString(work, pos)) { idx = pos + 1; continue; }
                    int startParen = pos + kv.Key.Length;
                    int endParen = FindMatchingParen(work, startParen);
                    if (endParen < 0) break;
                    string argsInside = work.Substring(startParen + 1, endParen - startParen - 1);
                    var args = SplitArgs(argsInside);
                    string expanded = ExpandFuncMacro(kv.Value, args);
                    work = work.Substring(0, pos) + expanded + work.Substring(endParen + 1);
                    idx = pos + expanded.Length;
                    changedFunc = true;
                }
            }
            if (!changedFunc) break;
        }

        var sorted = new List<Macro>(macros.Values);
        sorted.Sort((a, b) => b.Name.Length.CompareTo(a.Name.Length));
        for (int iter = 0; iter < 16; iter++)
        {
            bool changed = false;
            foreach (var macro in sorted)
            {
                if (macro.IsFunc) continue;
                if (string.IsNullOrEmpty(macro.Body)) continue;
                var rx = new Regex(@"\b" + Regex.Escape(macro.Name) + @"\b");
                var matches = rx.Matches(work);
                for (int mi = matches.Count - 1; mi >= 0; mi--)
                {
                    var m = matches[mi];
                    if (IsInsideString(work, m.Index)) continue;
                    work = work.Substring(0, m.Index) + (macro.Body ?? "") + work.Substring(m.Index + m.Length);
                    changed = true;
                }
            }
            if (!changed) break;
        }
        work = Regex.Replace(work, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        work = MergeAdjacentStrings(work);
        return work + commentPart;
    }

    // Merge adjacent string literals ("foo" "bar" -> "foobar") without
    // mistaking closing quotes for opening quotes. The old regex
    // "\"([^\"]*)\"\\s+\"([^\"]*)\"" matched from a *closing* quote
    // (e.g. in `"A=" + " " + "B"` it matched `" + " " + "`) and
    // collapsed the expression into `"A=" +  + "B"`, deleting the
    // middle string. This scan only merges when a closing quote is
    // followed by nothing but whitespace and then an opening quote.
    private static string MergeAdjacentStrings(string work)
    {
        var sb = new StringBuilder();
        int i = 0;
        while (i < work.Length)
        {
            char c = work[i];
            if (c == '"')
            {
                int end = FindStringEnd(work, i, '"');
                if (end < 0) { sb.Append(work.Substring(i)); break; }
                string content = work.Substring(i + 1, end - i - 1);
                int j = end + 1;
                while (true)
                {
                    int k = j;
                    while (k < work.Length && (work[k] == ' ' || work[k] == '\t')) k++;
                    if (k < work.Length && work[k] == '"')
                    {
                        int end2 = FindStringEnd(work, k, '"');
                        if (end2 < 0) break;
                        content += work.Substring(k + 1, end2 - k - 1);
                        j = end2 + 1;
                        continue;
                    }
                    break;
                }
                sb.Append('"');
                sb.Append(content);
                sb.Append('"');
                i = j;
            }
            else if (c == '\'')
            {
                // Char literal: copy verbatim so a '"' inside it (e.g. '"')
                // is never mistaken for a string delimiter.
                int end = FindStringEnd(work, i, '\'');
                if (end < 0) { sb.Append(c); i++; }
                else { sb.Append(work.Substring(i, end - i + 1)); i = end + 1; }
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }
        return sb.ToString();
    }

    private static int FindStringEnd(string s, int openQuote, char quote)
    {
        int i = openQuote + 1;
        while (i < s.Length)
        {
            if (s[i] == '\\') { i += 2; continue; }
            if (s[i] == quote) return i;
            if (s[i] == '\n') return -1;
            i++;
        }
        return -1;
    }

    private string ExpandFuncMacro(Macro macro, List<string> args)
    {
        string body = macro.Body ?? "";
        if (macro.Params != null)
        {
            for (int i = 0; i < macro.Params.Count && i < args.Count; i++)
            {
                string param = macro.Params[i];
                string arg = args[i];
                body = Regex.Replace(body, @"\b" + Regex.Escape(param) + @"\b", arg);
            }
        }
        // Neolith expand_define: ## is skipped, surrounding tokens concatenate.
        body = Regex.Replace(body, @"\s*##\s*", "");
        return body;
    }

    private int FindMatchingParen(string s, int openPos)
    {
        int depth = 0;
        bool inStr = false;
        char strChar = '\0';
        for (int i = openPos; i < s.Length; i++)
        {
            char c = s[i];
            if (!inStr && (c == '"' || c == '\''))
            {
                inStr = true; strChar = c;
            }
            else if (inStr && c == strChar)
            {
                if (i > 0 && s[i - 1] == '\\') continue;
                inStr = false;
            }
            else if (!inStr)
            {
                if (c == '(') depth++;
                else if (c == ')')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
        }
        return -1;
    }

    private List<string> SplitArgs(string inside)
    {
        var list = new List<string>();
        var sb = new StringBuilder();
        int depth = 0;
        bool inStr = false;
        char strChar = '\0';
        for (int i = 0; i < inside.Length; i++)
        {
            char c = inside[i];
            if (!inStr && (c == '"' || c == '\''))
            {
                inStr = true; strChar = c; sb.Append(c);
            }
            else if (inStr && c == strChar)
            {
                if (i > 0 && inside[i - 1] == '\\') { sb.Append(c); continue; }
                inStr = false; sb.Append(c);
            }
            else if (!inStr && c == '(') { depth++; sb.Append(c); }
            else if (!inStr && c == ')') { depth--; sb.Append(c); }
            else if (!inStr && c == ',' && depth == 0) { list.Add(sb.ToString().Trim()); sb.Clear(); }
            else sb.Append(c);
        }
        if (sb.Length > 0) list.Add(sb.ToString().Trim());
        return list;
    }
}
