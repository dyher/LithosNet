using System;
using System.Collections.Generic;
namespace LithosNet.Compiler;

public sealed class Parser
{
    private readonly Lexer _lexer;
    private readonly CompilerContext _ctx;
    private readonly string _fileName;
    private Token _current;
    private Token _prev;
    private readonly List<string> _errors = new();
    public IReadOnlyList<string> Errors => _errors;
    public bool HasErrors => _errors.Count > 0;
    public List<string> Inherits = new();
    public Parser(Lexer lexer, CompilerContext ctx, string fileName = "<lpc>") { _lexer = lexer ?? throw new ArgumentNullException(nameof(lexer)); _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx)); _fileName = fileName; _current = _lexer.NextToken(); _prev = _current; }
    private void Advance() { _prev = _current; _current = _lexer.NextToken(); }
    private bool Match(TokenType type, string text = null) { if (_current.Type != type) return false; if (text != null && _current.Text != text) return false; return true; }
    private bool Expect(TokenType type, string text = null) { if (!Match(type, text)) { _errors.Add($"{_fileName}:{_current.Line}: expected {type} {(text ?? "")} got {_current.Type} '{_current.Text}'"); return false; } Advance(); return true; }
    private bool IsTypeKeyword(string text) => text == "int" || text == "string" || text == "object" || text == "mapping" || text == "mixed" || text == "void" || text == "function";
    private bool IsEfunKeyword(string text) => text == "sizeof" || text == "keys" || text == "values" || text == "m_delete" || text == "map_delete" || text == "foreach" || text == "evaluate" || text == "filter" || text == "map" || text == "sort_array" || text == "clone_object";
    public List<ParseFunction> ParseFunctions()
    {
        var functions = new List<ParseFunction>();
        while (_current.Type != TokenType.Eof)
        {
            if (_current.Type == TokenType.Punct && (_current.Text == ";" || _current.Text == "}")) { Advance(); continue; }
            if (_current.Type == TokenType.Keyword && _current.Text == "inherit")
            {
                ParseInherit(); continue;
            }
            if (_current.Type == TokenType.Keyword && IsTypeKeyword(_current.Text)) { var f = ParseFunctionDef(); if (f != null) functions.Add(f); }
            else if (_current.Type == TokenType.Identifier || (_current.Type == TokenType.Keyword && !IsTypeKeyword(_current.Text) && _current.Text != "foreach"))
            {
                var f = ParseFunctionDef(); if (f != null) functions.Add(f); else Advance();
            }
            else if (_current.Type == TokenType.Keyword && _current.Text == "foreach") { Advance(); }
            else Advance();
        }
        return functions;
    }
    private void ParseInherit()
    {
        int line = _current.Line;
        Expect(TokenType.Keyword, "inherit");
        string path = "";
        if (_current.Type == TokenType.String) { path = _current.Text; Advance(); }
        else if (_current.Type == TokenType.Identifier) { path = _current.Text; Advance(); }
        if (Match(TokenType.Punct, ";")) Advance();
        if (!string.IsNullOrEmpty(path)) Inherits.Add(path);
    }
    private ParseFunction ParseFunctionDef() { int line = _current.Line; string returnType = "int"; if (_current.Type == TokenType.Keyword && IsTypeKeyword(_current.Text)) { returnType = _current.Text; Advance(); while (_current.Text == "*") { returnType += "*"; Advance(); } } if (_current.Type != TokenType.Identifier && !(_current.Type == TokenType.Keyword && IsEfunKeyword(_current.Text))) return null; if (_current.Text == "foreach") return null; string funcName = _current.Text; Advance(); Expect(TokenType.Punct, "("); var args = new List<ParseNode>(); while (!Match(TokenType.Punct, ")") && _current.Type != TokenType.Eof) { if (_current.Type == TokenType.Keyword && IsTypeKeyword(_current.Text)) { string atype = _current.Text; Advance(); while (_current.Text == "*") { atype += "*"; Advance(); } if (_current.Type == TokenType.Identifier) { string aname = _current.Text; Advance(); args.Add(new ParseNode { Kind = NodeKind.NODE_VAR_DEF, Name = aname, Line = line }); } } else if (_current.Type == TokenType.Identifier) { string aname = _current.Text; Advance(); args.Add(new ParseNode { Kind = NodeKind.NODE_VAR_DEF, Name = aname, Line = line }); } if (Match(TokenType.Punct, ",")) { Advance(); continue; } break; } Expect(TokenType.Punct, ")"); if (Match(TokenType.Punct, ";")) { Advance(); return new ParseFunction { Name = funcName, Line = line, Args = args, Body = null }; } var body = ParseBlock(); return new ParseFunction { Name = funcName, Line = line, Args = args, Body = body }; }
    private ParseNode ParseBlock() { Expect(TokenType.Punct, "{"); var block = new ParseNode { Kind = NodeKind.NODE_FUNCTION_DEF, Line = _current.Line }; while (!Match(TokenType.Punct, "}") && _current.Type != TokenType.Eof) { if (_current.Type == TokenType.Keyword && IsTypeKeyword(_current.Text)) { var varList = ParseVarDeclList(); if (varList != null) { foreach (var child in varList.Children) block.AddChild(child); continue; } } var stmt = ParseStatement(); if (stmt != null) block.AddChild(stmt); else Advance(); } Expect(TokenType.Punct, "}"); return block; }
    private ParseNode ParseVarDeclList(bool requireSemi = true)
    {
        int line = _current.Line; string typeName = _current.Text; Advance(); while (_current.Text == "*") { typeName += "*"; Advance(); }
        var listNode = new ParseNode { Kind = NodeKind.NODE_VAR_DEF, Line = line, Name = typeName };
        while (_current.Type == TokenType.Identifier)
        {
            string varName = _current.Text; int varLine = _current.Line; Advance();
            int arraySize = 0; bool isArray = false;
            if (Match(TokenType.Punct, "["))
            {
                Advance();
                if (_current.Type == TokenType.Number) { arraySize = (int)_current.NumberValue; Advance(); }
                Expect(TokenType.Punct, "]");
                isArray = true;
            }
            ParseNode initExpr = null;
            if (_current.Text == "=") { Advance(); initExpr = ParseExpression(); }
            var varNode = new ParseNode { Kind = NodeKind.NODE_VAR_DEF, Name = varName, Line = varLine };
            if (isArray) { var sizeNode = ParseNode.NewNumber(arraySize, varLine); sizeNode.Name = "__array_size"; varNode.AddChild(sizeNode); }
            if (initExpr != null) varNode.AddChild(initExpr);
            listNode.AddChild(varNode);
            if (Match(TokenType.Punct, ",")) { Advance(); continue; } break;
        }
        if (requireSemi && Match(TokenType.Punct, ";")) Advance();
        return listNode;
    }
    private ParseNode ParseStatement() {
        if (Match(TokenType.Keyword, "foreach")) { return ParseForeach(); }
        if (Match(TokenType.Keyword, "if")) { int line = _current.Line; Advance(); Expect(TokenType.Punct, "("); var cond = ParseExpression(); Expect(TokenType.Punct, ")"); var thenStmt = ParseStatement(); ParseNode elseStmt = null; if (Match(TokenType.Keyword, "else")) { Advance(); elseStmt = ParseStatement(); } var ifNode = new ParseNode { Kind = NodeKind.NODE_IF, Line = line }; if (cond != null) ifNode.AddChild(cond); if (thenStmt != null) ifNode.AddChild(thenStmt); if (elseStmt != null) ifNode.AddChild(elseStmt); return ifNode; } if (Match(TokenType.Keyword, "while")) { int line = _current.Line; Advance(); Expect(TokenType.Punct, "("); var cond = ParseExpression(); Expect(TokenType.Punct, ")"); var body = ParseStatement(); var whileNode = new ParseNode { Kind = NodeKind.NODE_LOOP, Line = line, Name = "while" }; if (cond != null) whileNode.AddChild(cond); if (body != null) whileNode.AddChild(body); return whileNode; } if (Match(TokenType.Keyword, "for")) { int line = _current.Line; Advance(); Expect(TokenType.Punct, "("); ParseNode init = null; if (Match(TokenType.Punct, ";")) { Advance(); } else if (_current.Type == TokenType.Keyword && IsTypeKeyword(_current.Text)) { init = ParseVarDeclList(true); } else { init = ParseExpression(); Expect(TokenType.Punct, ";"); } ParseNode cond = null; if (Match(TokenType.Punct, ";")) { Advance(); } else { cond = ParseExpression(); Expect(TokenType.Punct, ";"); } ParseNode inc = null; if (Match(TokenType.Punct, ")")) { } else { inc = ParseExpression(); } Expect(TokenType.Punct, ")"); var body = ParseStatement(); var forNode = new ParseNode { Kind = NodeKind.NODE_LOOP, Line = line, Name = "for" }; var newChildren = new List<ParseNode>(); newChildren.Add(init ?? new ParseNode { Kind = NodeKind.NODE_NUMBER, NumberValue = 0, Name = "__empty_init" }); newChildren.Add(cond ?? new ParseNode { Kind = NodeKind.NODE_NUMBER, NumberValue = 1, Name = "__empty_cond" }); newChildren.Add(inc ?? new ParseNode { Kind = NodeKind.NODE_NUMBER, NumberValue = 0, Name = "__empty_inc" }); if (body != null) newChildren.Add(body); forNode.Children = newChildren; return forNode; } if (Match(TokenType.Keyword, "break")) { int line = _current.Line; Advance(); if (Match(TokenType.Punct, ";")) Advance(); return new ParseNode { Kind = NodeKind.NODE_LOOP, Line = line, Name = "break" }; } if (Match(TokenType.Keyword, "continue")) { int line = _current.Line; Advance(); if (Match(TokenType.Punct, ";")) Advance(); return new ParseNode { Kind = NodeKind.NODE_LOOP, Line = line, Name = "continue" }; } if (Match(TokenType.Keyword, "return")) { int line = _current.Line; Advance(); ParseNode expr = null; if (!Match(TokenType.Punct, ";")) expr = ParseExpression(); if (Match(TokenType.Punct, ";")) Advance(); var ret = new ParseNode { Kind = NodeKind.NODE_RETURN, Line = line }; if (expr != null) ret.AddChild(expr); return ret; } if (Match(TokenType.Punct, "{")) return ParseBlock(); var e = ParseExpression(); if (e != null) { if (Match(TokenType.Punct, ";")) Advance(); return e; } return null; }
    private ParseNode ParseForeach()
    {
        int line = _current.Line;
        Expect(TokenType.Keyword, "foreach");
        Expect(TokenType.Punct, "(");
        string keyName = null;
        string valName = null;
        ParseNode container = null;
        if (_current.Type == TokenType.Keyword && IsTypeKeyword(_current.Text)) Advance();
        if (_current.Type == TokenType.Identifier) { keyName = _current.Text; Advance(); }
        if (Match(TokenType.Punct, ",")) { Advance(); if (_current.Type == TokenType.Keyword && IsTypeKeyword(_current.Text)) Advance(); if (_current.Type == TokenType.Identifier) { valName = _current.Text; Advance(); } }
        if (_current.Type == TokenType.Identifier && _current.Text == "in") Advance();
        else if (_current.Type == TokenType.Keyword && _current.Text == "in") Advance();
        container = ParseExpression();
        Expect(TokenType.Punct, ")");
        var body = ParseStatement();
        var foreachNode = new ParseNode { Kind = NodeKind.NODE_LOOP, Name = "foreach", Line = line };
        var keyNode = keyName != null ? ParseNode.NewIdentifier(keyName, line) : null;
        var valNode = valName != null ? ParseNode.NewIdentifier(valName, line) : null;
        if (keyNode != null) foreachNode.AddChild(keyNode);
        if (valNode != null) foreachNode.AddChild(valNode);
        if (container != null) foreachNode.AddChild(container);
        if (body != null) foreachNode.AddChild(body);
        return foreachNode;
    }
    private ParseNode ParseClosure()
    {
        int line = _current.Line;
        Expect(TokenType.Punct, "(:");
        var closureNode = new ParseNode { Kind = NodeKind.NODE_CLOSURE, Line = line };
        if (Match(TokenType.Punct, "{"))
        {
            var block = ParseBlock();
            closureNode.AddChild(block);
        }
        else
        {
            var expr = ParseExpression();
            if (expr != null) closureNode.AddChild(expr);
            while (Match(TokenType.Punct, ";"))
            {
                Advance();
                if (Match(TokenType.Punct, ":)")) break;
                var nextExpr = ParseExpression();
                if (nextExpr != null) closureNode.AddChild(nextExpr);
                else break;
            }
        }
        Expect(TokenType.Punct, ":)");
        return closureNode;
    }
    private ParseNode ParseExpression(int minPrec = 0) { var left = ParseUnary(); if (left == null) return null; while (true) { string op = _current.Text; int prec = GetPrecedence(op); if (prec < minPrec || prec < 0) break; if (_current.Text == "->" ) { Advance(); if (_current.Type != TokenType.Identifier) { _errors.Add($"{_fileName}:{_current.Line}: expected method name after ->"); break; } string method = _current.Text; Advance(); List<ParseNode> args = new List<ParseNode>(); if (Match(TokenType.Punct, "(")) { Advance(); while (!Match(TokenType.Punct, ")") && _current.Type != TokenType.Eof) { var arg = ParseExpression(); if (arg != null) args.Add(arg); if (Match(TokenType.Punct, ",")) Advance(); else break; } Expect(TokenType.Punct, ")"); } left = ParseNode.NewDotCall(left, method, args, left.Line); continue; } if (IsBinaryOp(op)) { Advance(); int nextMin = (op == "=" || op.EndsWith("=")) ? prec : prec + 1; var right = ParseExpression(nextMin); ParseNode bin; if (op == "=" || op.EndsWith("="))
                {
                    if (op == "+=" || op == "-=" || op == "*=" || op == "/=" || op == "%=" || op == "&=" || op == "|=" || op == "^=" || op == "<<=" || op == ">>=")
                    {
                        string baseOp = op.Substring(0, op.Length-1);
                        var baseBin = new ParseNode { Kind = NodeKind.NODE_BINARY_OP, Name = baseOp, Line = left.Line, Left = left, Right = right };
                        baseBin.Children.Add(left);
                        if (right != null) baseBin.Children.Add(right);
                        bin = new ParseNode { Kind = NodeKind.NODE_ASSIGN, Name = "=", Line = left.Line, Left = left, Right = baseBin };
                        bin.Children.Add(left);
                        bin.Children.Add(baseBin);
                    }
                    else { bin = new ParseNode { Kind = NodeKind.NODE_ASSIGN, Name = op, Line = left.Line, Left = left, Right = right }; bin.Children.Add(left); if (right != null) bin.Children.Add(right); }
                }
                else { bin = new ParseNode { Kind = NodeKind.NODE_BINARY_OP, Name = op, Line = left.Line, Left = left, Right = right }; bin.Children.Add(left); if (right != null) bin.Children.Add(right); } left = bin; continue; } break; } return left; }
    private ParseNode ParseUnary()
    {
        if (_current.Text == "++" || _current.Text == "--" || _current.Text == "!" || _current.Text == "~" || _current.Text == "-" || _current.Text == "+")
        {
            string op = _current.Text; int line = _current.Line; Advance();
            var operand = ParseUnary();
            var node = new ParseNode { Kind = NodeKind.NODE_BINARY_OP, Name = op, Line = line }; node.Left = operand; if (operand != null) node.Children.Add(operand); return node;
        }
        var primary = ParsePrimary();
        if (primary != null && (_current.Text == "++" || _current.Text == "--"))
        {
            string op = _current.Text == "++" ? "post++" : "post--"; int line = _current.Line; Advance();
            var node = new ParseNode { Kind = NodeKind.NODE_BINARY_OP, Name = op, Line = line }; node.Left = primary; node.Children.Add(primary); return node;
        }
        return primary;
    }
    private bool IsBinaryOp(string op) { switch (op) { case "*": case "/": case "%": case "+": case "-": case "<<": case ">>": case "<": case "<=": case ">": case ">=": case "==": case "!=": case "&": case "^": case "|": case "&&": case "||": case "=": case "+=": case "-=": case "*=": case "/=": case "%=": case "&=": case "|=": case "^=": case "<<=": case ">>=": return true; default: return false; } }
    private int GetPrecedence(string op) { switch (op) { case "*": case "/": case "%": return 9; case "+": case "-": return 8; case "<<": case ">>": return 7; case "<": case "<=": case ">": case ">=": return 6; case "==": case "!=": return 5; case "&": return 4; case "^": return 3; case "|": return 2; case "&&": case "||": return 1; case "=": case "+=": case "-=": case "*=": case "/=": case "%=": case "&=": case "|=": case "^=": case "<<=": case ">>=": return 0; default: return -1; } }
    private ParseNode ParsePrimary()
    {
        if (Match(TokenType.Punct, "(:")) { return ParseClosure(); }
        if (Match(TokenType.Punct, "("))
        {
            try {
                var f = _lexer.GetType().GetField("_src", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var p = _lexer.GetType().GetField("_pos", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f!=null && p!=null) {
                    string src = (string)f.GetValue(_lexer);
                    int pos = (int)p.GetValue(_lexer);
                    int pp = pos;
                    while (pp < src.Length && char.IsWhiteSpace(src[pp])) pp++;
                    if (pp < src.Length && src[pp]=='{' )
                    {
                        int line = _current.Line; Advance(); Expect(TokenType.Punct, "{");
                        var arrNode = new ParseNode { Kind = NodeKind.NODE_ARRAY, Name = "array_lit", Line = line };
                        while (!Match(TokenType.Punct, "}") && _current.Type != TokenType.Eof) { var elem = ParseExpression(); if (elem != null) arrNode.AddChild(elem); if (Match(TokenType.Punct, ",")) { Advance(); continue; } break; }
                        Expect(TokenType.Punct, "}"); Expect(TokenType.Punct, ")"); return TryParseIndex(arrNode);
                    }
                    if (pp < src.Length && src[pp]=='[')
                    {
                        int line = _current.Line; Advance(); Expect(TokenType.Punct, "[");
                        var mapNode = new ParseNode { Kind = NodeKind.NODE_MAPPING, Line = line };
                        while (!Match(TokenType.Punct, "]") && _current.Type != TokenType.Eof) { var key = ParseExpression(); if (Match(TokenType.Punct, ":") || Match(TokenType.Operator, ":")) Advance(); else { _errors.Add($"{_fileName}:{_current.Line}: expected : in mapping"); break; } var val = ParseExpression(); if (key != null) mapNode.AddChild(key); if (val != null) mapNode.AddChild(val); if (Match(TokenType.Punct, ",")) { Advance(); continue; } break; }
                        Expect(TokenType.Punct, "]"); Expect(TokenType.Punct, ")"); return TryParseIndex(mapNode);
                    }
                }
            } catch {}
            Advance(); var expr = ParseExpression(); Expect(TokenType.Punct, ")"); return TryParseIndex(expr);
        }
        if (_current.Type == TokenType.Number) { var node = ParseNode.NewNumber(_current.NumberValue, _current.Line); Advance(); return TryParseIndex(node); }
        if (_current.Type == TokenType.Real) { var node = ParseNode.NewReal(_current.RealValue, _current.Line); Advance(); return TryParseIndex(node); }
        if (_current.Type == TokenType.String) { var s = _current.Text ?? ""; var node = ParseNode.NewString(s, s.Length, _current.Line); Advance(); return TryParseIndex(node); }
        if (_current.Type == TokenType.Identifier || (_current.Type == TokenType.Keyword && IsEfunKeyword(_current.Text)))
        {
            if (_current.Text == "in") return null;
            var ident = ParseNode.NewIdentifier(_current.Text, _current.Line); Advance();
            ident = TryParseIndex(ident);
            if (Match(TokenType.Punct, "("))
            {
                Advance(); var args = new List<ParseNode>(); while (!Match(TokenType.Punct, ")") && _current.Type != TokenType.Eof) { var arg = ParseExpression(); if (arg != null) args.Add(arg); if (Match(TokenType.Punct, ",")) Advance(); else break; } Expect(TokenType.Punct, ")"); var call = new ParseNode { Kind = NodeKind.NODE_CALL, Name = ident.Name, Line = ident.Line }; foreach (var a in args) call.AddChild(a); return TryParseIndex(call);
            }
            if (_current.Text == "->") { Advance(); if (_current.Type != TokenType.Identifier) { _errors.Add($"{_fileName}:{_current.Line}: expected method after ->"); return ident; } string method = _current.Text; Advance(); var args = new List<ParseNode>(); if (Match(TokenType.Punct, "(")) { Advance(); while (!Match(TokenType.Punct, ")") && _current.Type != TokenType.Eof) { var arg = ParseExpression(); if (arg != null) args.Add(arg); if (Match(TokenType.Punct, ",")) Advance(); else break; } Expect(TokenType.Punct, ")"); } var dot = ParseNode.NewDotCall(ident, method, args, ident.Line); return TryParseIndex(dot); }
            return ident;
        }
        if (_current.Type == TokenType.Keyword && (_current.Text == "sizeof" || _current.Text == "evaluate" || _current.Text == "filter" || _current.Text == "map" || _current.Text == "sort_array" || _current.Text == "clone_object"))
        {
            string name = _current.Text; int line = _current.Line; Advance();
            if (Match(TokenType.Punct, "(")) { Advance(); var args = new List<ParseNode>(); while (!Match(TokenType.Punct, ")") && _current.Type != TokenType.Eof) { var arg = ParseExpression(); if (arg != null) args.Add(arg); if (Match(TokenType.Punct, ",")) Advance(); else break; } Expect(TokenType.Punct, ")"); var call = new ParseNode { Kind = NodeKind.NODE_CALL, Name = name, Line = line }; foreach (var a in args) call.AddChild(a); return TryParseIndex(call); }
            var id = ParseNode.NewIdentifier(name, line); return TryParseIndex(id);
        }
        if (Match(TokenType.Punct, "(")) { Advance(); var expr = ParseExpression(); Expect(TokenType.Punct, ")"); return TryParseIndex(expr); }
        return null;
    }
    private ParseNode TryParseIndex(ParseNode baseNode)
    {
        if (baseNode == null) return null;
        while (Match(TokenType.Punct, "["))
        {
            Advance(); var idx = ParseExpression(); Expect(TokenType.Punct, "]");
            var indexNode = new ParseNode { Kind = NodeKind.NODE_ARRAY, Name = "[]", Line = baseNode.Line, Left = baseNode, Right = idx };
            indexNode.Children.Add(baseNode); if (idx != null) indexNode.Children.Add(idx); baseNode = indexNode;
        }
        return baseNode;
    }
}
