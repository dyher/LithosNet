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
        "int","string","object","mapping","mixed","void","inherit","private","protected","public",
        "static","nomask","varargs","class","struct","new","sizeof","typeof","in","keys","values","m_delete","map_delete","evaluate","filter","map","sort_array","clone_object"
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
        if (c == '"') { Next(); var sb = new StringBuilder(); while (Peek() != '"' && Peek() != '\0' && Peek() != '\n') { char ch = Next(); if (ch == '\\') { char esc = Next(); switch (esc) { case 'n': sb.Append('\n'); break; case 't': sb.Append('\t'); break; case 'r': sb.Append('\r'); break; case '"': sb.Append('"'); break; case '\\': sb.Append('\\'); break; default: sb.Append(esc); break; } } else sb.Append(ch); } if (Peek() == '"') Next(); string s = sb.ToString(); return new Token { Type = TokenType.String, Text = s, Line = line, Column = col, Length = s.Length }; }
        string three = new string(new char[]{c, Peek(1), Peek(2)});
        if (three == "<<=" || three == ">>=") { Next(); Next(); Next(); return new Token { Type = TokenType.Operator, Text = three, Line = line, Column = col }; }
        string two = new string(new char[]{c, Peek(1)});
        if (two == "(:" || two == ":)" || two == "::") { Next(); Next(); return new Token { Type = TokenType.Punct, Text = two, Line = line, Column = col }; }
        if (two == "++" || two == "--" || two == "==" || two == "!=" || two == "<=" || two == ">=" || two == "&&" || two == "||" || two == "<<" || two == ">>" || two == "->" || two == "+=" || two == "-=" || two == "*=" || two == "/=" || two == "%=" || two == "&=" || two == "|=" || two == "^=") { Next(); Next(); TokenType tt = TokenType.Punct; if (two.EndsWith("=") && two != "==" && two != "!=" && two != "<=" && two != ">=") tt = TokenType.Operator; return new Token { Type = tt, Text = two, Line = line, Column = col }; }
        Next(); string txt1 = c.ToString(); TokenType ttype = TokenType.Punct; if ("+-*/%&|^~!<>=".Contains(c)) ttype = TokenType.Operator; return new Token { Type = ttype, Text = txt1, Line = line, Column = col };
    }
}
