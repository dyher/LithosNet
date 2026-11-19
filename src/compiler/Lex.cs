using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LithosNet.Compiler;

public enum TokenType
{
    Eof, Identifier, Number, Real, String, Keyword, Operator, Punct, Include, Define
}

public struct Token
{
    public TokenType Type;
    public string Text;
    public long NumberValue;
    public double RealValue;
    public int Line;
    public int Column;
    public int Length;
    public override string ToString() => $"{Type} {Text} @{Line}:{Column}";
}

public sealed class Lexer
{
    private readonly string _src;
    private readonly string _fileName;
    private int _pos;
    private int _line = 1;
    private int _col = 1;
    private readonly List<string> _includeDirs;
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "if","else","for","foreach","while","do","switch","case","default","break","continue","return",
        "int","string","object","mapping","mixed","void","closure","function","float","status","buffer","inherit","private","protected","public",
        "static","nomask","varargs","class","struct","new","catch","sizeof","typeof","in","keys","values","m_delete","map_delete","evaluate","filter","map","sort_array","clone_object"
    };
    public Lexer(string source, string fileName, List<string> includeDirs = null!)
    {
        _src = source ?? ""; _fileName = fileName ?? "<input>"; _includeDirs = includeDirs ?? new List<string>();
    }
    public int Line => _line;
    public static List<string> NormalizeIncludeList(List<string> dirs, string root)
    {
        var result = new List<string>(); if (dirs == null) return result;
        foreach (var d in dirs) { if (string.IsNullOrWhiteSpace(d)) continue; try { string full = Path.IsPathRooted(d) ? d : Path.Combine(root ?? "", d); result.Add(Path.GetFullPath(full)); } catch { result.Add(d); } } return result;
    }
    public static bool IsPathWithinRoot(string path, string root)
    {
        try { string fullPath = Path.GetFullPath(path); string fullRoot = Path.GetFullPath(root ?? ""); return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase); } catch { return true; }
    }
    private char Peek(int off = 0) { int p = _pos + off; return p < _src.Length ? _src[p] : '\0'; }
    private void AppendEscape(System.Text.StringBuilder sb)
    {
        char esc = Next();
        if (esc == 'x' || esc == 'X') {
            int val = 0; int cnt = 0;
            while (cnt < 2 && Peek() != '\0' && Uri.IsHexDigit(Peek())) { char h = Next(); val = val * 16 + (h <= '9' ? h - '0' : (h <= 'F' ? h - 'A' + 10 : h - 'a' + 10)); cnt++; }
            if (cnt == 0) sb.Append('x'); else sb.Append((char)val);
            return;
        }
        if (esc >= '0' && esc <= '7') {
            int val = esc - '0'; int cnt = 1;
            while (cnt < 3 && Peek() >= '0' && Peek() <= '7') { val = val * 8 + (Next() - '0'); cnt++; }
            sb.Append((char)val); return;
        }
        switch (esc) {
            case 'n': sb.Append('\n'); break;
            case 't': sb.Append('\t'); break;
            case 'r': sb.Append('\r'); break;
            case 'a': sb.Append('\a'); break;
            case 'b': sb.Append('\b'); break;
            case 'f': sb.Append('\f'); break;
            case 'v': sb.Append('\v'); break;
            case 'e': sb.Append((char)27); break;
            case '?': sb.Append('?'); break;
            case '\'': sb.Append('\''); break;
            case '"': sb.Append('"'); break;
            case '\\': sb.Append('\\'); break;
            default: sb.Append(esc); break;
        }
    }
    private char Next() { if (_pos >= _src.Length) return '\0'; char c = _src[_pos++]; if (c == '\n') { _line++; _col = 1; } else { _col++; } return c; }
    private void SkipWhitespaceAndComments()
    {
        while (true)
        {
            char c = Peek();
            if (c == ' ' || c == '\t' || c == '\r' || c == '\n') { Next(); continue; }
            if (c == '/' && Peek(1) == '/') { while (Peek() != '\n' && Peek() != '\0') Next(); continue; }
            if (c == '/' && Peek(1) == '*') { Next(); Next(); while (!(Peek() == '*' && Peek(1) == '/') && Peek() != '\0') Next(); if (Peek() != '\0') { Next(); Next(); } continue; }
            if (c == '#' && _col == 1) { while (Peek() != '\n' && Peek() != '\0') Next(); continue; }
            break;
        }
    }
    private static bool IsHexDigit(char ch) => (ch>='0'&&ch<='9')||(ch>='a'&&ch<='f')||(ch>='A'&&ch<='F');
    public Token NextToken()
    {
        SkipWhitespaceAndComments();
        int line = _line; int col = _col; char c = Peek();
        if (c == '\0') return new Token { Type = TokenType.Eof, Line = line, Column = col };
        if (c == '$' && char.IsDigit(Peek(1))) { int start = _pos; Next(); while (char.IsDigit(Peek())) Next(); string text = _src.Substring(start, _pos - start); return new Token { Type = TokenType.Identifier, Text = text, Line = line, Column = col }; }
        if (char.IsLetter(c) || c == '_') { int start = _pos; while (char.IsLetterOrDigit(Peek()) || Peek() == '_') Next(); string text = _src.Substring(start, _pos - start); var type = Keywords.Contains(text) ? TokenType.Keyword : TokenType.Identifier; return new Token { Type = type, Text = text, Line = line, Column = col }; }
        if (char.IsDigit(c))
        {
            int start = _pos; bool isHex = false; bool isReal = false;
            if (c == '0' && (Peek(1) == 'x' || Peek(1) == 'X')) { isHex = true; Next(); Next(); while (IsHexDigit(Peek())) Next(); }
            else { while (char.IsDigit(Peek())) Next(); if (Peek() == '.' && char.IsDigit(Peek(1))) { isReal = true; Next(); while (char.IsDigit(Peek())) Next(); } if (Peek() == 'e' || Peek() == 'E') { isReal = true; Next(); if (Peek() == '+' || Peek() == '-') Next(); while (char.IsDigit(Peek())) Next(); } }
            string txt = _src.Substring(start, _pos - start);
            if (isReal) { if (double.TryParse(txt, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double rv)) return new Token { Type = TokenType.Real, Text = txt, RealValue = rv, Line = line, Column = col }; }
            else { long val = 0; try { if (isHex) { string hexPart = txt.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? txt.Substring(2) : txt; val = Convert.ToInt64(hexPart, 16); } else { if (txt.Length > 1 && txt[0] == '0') { bool isOctal = true; foreach (char ch in txt) if (ch < '0' || ch > '7') { isOctal = false; break; } if (isOctal) val = Convert.ToInt64(txt, 8); else val = long.Parse(txt, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture); } else val = long.Parse(txt, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture); } } catch { val = 0; } return new Token { Type = TokenType.Number, Text = txt, NumberValue = val, Line = line, Column = col }; }
        }
        if (c == '@')
        {
            // Check for heredoc @IDENT
            int savePos = _pos;
            int saveLine = _line;
            int saveCol = _col;
            Next(); // consume @
            int idStart = _pos;
            if (char.IsLetter(Peek()) || Peek() == '_')
            {
                while (char.IsLetterOrDigit(Peek()) || Peek() == '_') Next();
                string label = _src.Substring(idStart, _pos - idStart);
                // Skip to end of line
                while (Peek() != '\n' && Peek() != '\0') Next();
                if (Peek() == '\n') Next();
                // Now collect lines until a line that is exactly label (trimmed)
                var contentSb = new StringBuilder();
                while (true)
                {
                    if (_pos >= _src.Length) break;
                    int lineStart = _pos;
                    // read until newline
                    while (Peek() != '\n' && Peek() != '\0') Next();
                    string lineText = _src.Substring(lineStart, _pos - lineStart);
                    if (Peek() == '\n') Next();
                    string trimmed = lineText.Trim();
                    if (trimmed == label)
                    {
                        // terminator found, stop
                        break;
                    }
                    contentSb.Append(lineText);
                    contentSb.Append('\n');
                }
                string heredoc = contentSb.ToString();
                return new Token { Type = TokenType.String, Text = heredoc, Line = line, Column = col, Length = heredoc.Length };
            }
            else
            {
                // Not a heredoc, restore and treat @ as punct
                _pos = savePos;
                _line = saveLine;
                _col = saveCol;
            }
        }
        if (c == '"') {
            // parse first string, then concatenate adjacent strings (C-style)
            string combined = "";
            int firstLine = line;
            int firstCol = col;
            while (true)
            {
                // parse one string literal at current pos (must start with ")
                if (Peek() != '"')
                {
                    break;
                }
                Next(); // opening "
                var sb = new StringBuilder();
                while (Peek() != '"' && Peek() != '\0')
                {
                    // allow newline inside? LPC strings normally not, but we allow if escaped? We'll break on newline unless escaped?
                    // For adjacent string concat across lines, the next string starts after whitespace, not inside current.
                    // So if we encounter newline without closing quote, treat as end (will be handled by outer loop after whitespace skip)
                    if (Peek() == '\n')
                    {
                        // Unterminated string on this line -> break and let outer handle? But for now break
                        break;
                    }
                    char ch = Next();
                    if (ch == '\\') { AppendEscape(sb); }
                    else sb.Append(ch);
                }
                if (Peek() == '"') Next(); // closing "
                combined += sb.ToString();
                // After this string, skip whitespace and comments to check if next non-whitespace is another "
                int savedPos2 = _pos;
                int savedLine2 = _line;
                int savedCol2 = _col;
                SkipWhitespaceAndComments();
                if (Peek() == '"')
                {
                    // continue loop to concatenate
                    continue;
                }
                else
                {
                    // restore to after whitespace? Actually we want to keep whitespace consumed? For token stream, whitespace is skipped anyway, so we can keep position after skip
                    // But we already skipped whitespace; if next is not string, we should leave pos at after whitespace (which Skip does)
                    // However we saved pos before skip; we want to keep skipped pos if not string? Let's keep the skipped pos (so next token starts correctly)
                    // If we restore, whitespace would be re-skipped anyway. So keep current pos.
                    break;
                }
            }
            return new Token { Type = TokenType.String, Text = combined, Line = firstLine, Column = firstCol, Length = combined.Length };
        }
        if (c == '\'')
        {
            Next();
            var sb = new StringBuilder();
            while (Peek() != '\'' && Peek() != '\0' && Peek() != '\n')
            {
                char ch = Next();
                if (ch == '\\') { AppendEscape(sb); }
                else sb.Append(ch);
            }
            if (Peek() == '\'') Next();
            string s = sb.ToString();
            // LPC char literal is an integer (the character code), not a string.
            // Neolith lex.c: yylval.number = <char code>; return L_NUMBER.
            // Multi-char literal: Neolith errors; we take the first char (lenient).
            long code = s.Length > 0 ? (long)s[0] : 0;
            return new Token { Type = TokenType.Number, Text = s, NumberValue = code, Line = line, Column = col };
        }
        string three = new string(new char[]{c, Peek(1), Peek(2)});
        if (three == "<<=" || three == ">>=") { Next(); Next(); Next(); return new Token { Type = TokenType.Operator, Text = three, Line = line, Column = col }; }
        string two = new string(new char[]{c, Peek(1)});
        if (two == "..") { Next(); Next(); return new Token { Type = TokenType.Punct, Text = "..", Line = line, Column = col }; }
        if (two == "(:" || two == ":)" || two == "::") { Next(); Next(); return new Token { Type = TokenType.Punct, Text = two, Line = line, Column = col }; }
        if (two == "++" || two == "--" || two == "==" || two == "!=" || two == "<=" || two == ">=" || two == "&&" || two == "||" || two == "<<" || two == ">>" || two == "->" || two == "+=" || two == "-=" || two == "*=" || two == "/=" || two == "%=" || two == "&=" || two == "|=" || two == "^=") { Next(); Next(); TokenType tt = TokenType.Punct; if (two.EndsWith("=") && two != "==" && two != "!=" && two != "<=" && two != ">=") tt = TokenType.Operator; return new Token { Type = tt, Text = two, Line = line, Column = col }; }
        Next(); string txt1 = c.ToString(); TokenType ttype = TokenType.Punct; if ("+-*/%&|^~!<>=".Contains(c)) ttype = TokenType.Operator; return new Token { Type = ttype, Text = txt1, Line = line, Column = col };
    }
}
