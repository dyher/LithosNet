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
    public List<GlobalDecl> Globals = new();
    public Parser(Lexer lexer, CompilerContext ctx, string fileName = "<lpc>") { _lexer = lexer ?? throw new ArgumentNullException(nameof(lexer)); _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx)); _fileName = fileName; _current = _lexer.NextToken(); _prev = _current; }
    private void Advance() { _prev = _current; _current = _lexer.NextToken(); }
    private bool Match(TokenType type, string text = null) { if (_current.Type != type) return false; if (text != null && _current.Text != text) return false; return true; }
    private bool MatchText(string text) { return _current.Text == text; }
    private bool Expect(TokenType type, string text = null) { if (!Match(type, text)) { _errors.Add($"{_fileName}:{_current.Line}: expected {type} {(text ?? "")} got {_current.Type} '{_current.Text}'"); return false; } Advance(); return true; }
    private bool IsTypeKeyword(string text) => text == "int" || text == "string" || text == "object" || text == "mapping" || text == "mixed" || text == "void" || text == "function" || text == "closure" || text == "float" || text == "status";
        private bool IsEfunKeyword(string text) => text == "sizeof" || text == "keys" || text == "values" || text == "m_delete" || text == "map_delete" || text == "foreach" || text == "evaluate" || text == "filter" || text == "map" || text == "sort_array" || text == "clone_object" || text == "write" || text == "sscanf" || text == "explode" || text == "implode" || text == "sprintf" || text == "read_file" || text == "write_file" || text == "file_size" || text == "get_dir" || text == "stat" || text == "rm" || text == "mkdir" || text == "cp" || text == "rename" || text == "debug_message" || text == "error" || text == "throw" || text == "call_out" || text == "remove_call_out" || text == "find_call_out" || text == "set_heart_beat" || text == "query_heart_beat" || text == "destruct" || text == "find_object" || text == "present" || text == "all_inventory" || text == "environment" || text == "first_inventory" || text == "next_inventory" || text == "deep_inventory" || text == "users" || text == "this_player" || text == "this_object" || text == "previous_object" || text == "objectp" || text == "stringp" || text == "intp" || text == "floatp" || text == "mapp" || text == "arrayp" || text == "functionp" || text == "bufferp" || text == "classp" || text == "nullp" || text == "undefinedp" || text == "typeof" || text == "crypt" || text == "old_crypt" || text == "oldcrypt" || text == "regexp" || text == "reg_assoc" || text == "crc32" || text == "c_str" || text == "envsubst" || text == "strwrap" || text == "match_path" || text == "to_int" || text == "to_float" || text == "to_string" || text == "replace_string" || text == "lower_case" || text == "upper_case" || text == "capitalize" || text == "trim" || text == "strlen" || text == "strsrch" || text == "member_array" || text == "shuffle" || text == "sort_array" || text == "unique_array" || text == "filter_array" || text == "map_array" || text == "copy" || text == "allocate" || text == "new";
    public List<ParseFunction> ParseFunctions()
    {
        var functions = new List<ParseFunction>();
        while (_current.Type != TokenType.Eof)
        {
            if (_current.Type == TokenType.Punct && (_current.Text == ";" || _current.Text == "}")) { Advance(); continue; }
            if (_current.Type == TokenType.Keyword && _current.Text == "inherit") { ParseInherit(); continue; }
            if (_current.Type == TokenType.Keyword && IsTypeKeyword(_current.Text)) { var f = ParseFunctionDef(); if (f != null) functions.Add(f); }
            else if (_current.Type == TokenType.Identifier || (_current.Type == TokenType.Keyword && !IsTypeKeyword(_current.Text) && _current.Text != "foreach" && _current.Text != "switch" && _current.Text != "case" && _current.Text != "default"))
            {
                var f = ParseFunctionDef(); if (f != null) functions.Add(f); else Advance();
            }
            else Advance();
        }
        return functions;
    }
    private void ParseInherit()
    {
        Expect(TokenType.Keyword, "inherit");
        string path = "";
        if (_current.Type == TokenType.String) { path = _current.Text; Advance(); }
        else if (_current.Type == TokenType.Identifier) { path = _current.Text; Advance(); }
        if (Match(TokenType.Punct, ";")) Advance();
        if (!string.IsNullOrEmpty(path)) Inherits.Add(path);
    }
    private bool IsModifier(string t) => t=="private"||t=="public"||t=="protected"||t=="static"||t=="nomask"||t=="varargs"||t=="override"||t=="nosave";
    private void SkipModifiers(){ while(_current.Type==TokenType.Keyword && IsModifier(_current.Text)) Advance(); }
    private ParseFunction ParseFunctionDef() {
        SkipModifiers();
        int line = _current.Line; string returnType = "int";
        if (_current.Type == TokenType.Keyword && IsTypeKeyword(_current.Text)) { returnType = _current.Text; Advance(); while (_current.Text == "*") { returnType += "*"; Advance(); } }
        // also handle implicit int when no type but modifiers skipped already
        if (_current.Type != TokenType.Identifier && !(_current.Type == TokenType.Keyword && IsEfunKeyword(_current.Text))) return null;
        if (_current.Text == "foreach" || _current.Text == "switch") return null;
        string funcName = _current.Text; Advance();
        if (!Match(TokenType.Punct, "(")) {
            // global variable declaration: funcName is first var name
            // parse array suffix
            if (Match(TokenType.Punct, "[")) {
                Advance();
                if (_current.Type == TokenType.Number) Advance();
                Expect(TokenType.Punct, "]");
            }
            ParseNode gInit = null;
            if (_current.Text == "=") {
                Advance();
                gInit = ParseExpression();
            }
            Globals.Add(new GlobalDecl { Name = funcName, Init = gInit });
            while (Match(TokenType.Punct, ",")) {
                Advance();
                if (_current.Type == TokenType.Identifier) {
                    string gName2 = _current.Text;
                    Advance();
                    if (Match(TokenType.Punct, "[")) {
                        Advance();
                        if (_current.Type == TokenType.Number) Advance();
                        Expect(TokenType.Punct, "]");
                    }
                    ParseNode gInit2 = null;
                    if (_current.Text == "=") {
                        Advance();
                        gInit2 = ParseExpression();
                    }
                    Globals.Add(new GlobalDecl { Name = gName2, Init = gInit2 });
                } else break;
            }
            if (Match(TokenType.Punct, ";")) Advance();
            else {
                // try to recover to next semicolon or brace
                while (_current.Type != TokenType.Eof && _current.Text != ";" && _current.Text != "}" && _current.Text != "{") Advance();
                if (_current.Text == ";") Advance();
            }
            return null;
        }
        Expect(TokenType.Punct, "(");
        var args = new List<ParseNode>();
        while (!Match(TokenType.Punct, ")") && _current.Type != TokenType.Eof) {
            SkipModifiers();
            if (_current.Type == TokenType.Keyword && IsTypeKeyword(_current.Text)) {
                string atype = _current.Text; Advance(); while (_current.Text == "*") { atype += "*"; Advance(); }
                if (_current.Type == TokenType.Identifier) { string aname = _current.Text; Advance(); args.Add(new ParseNode { Kind = NodeKind.NODE_VAR_DEF, Name = aname, Line = line }); }
                else { break; }
            } else if (_current.Type == TokenType.Identifier) {
                string aname = _current.Text; Advance(); args.Add(new ParseNode { Kind = NodeKind.NODE_VAR_DEF, Name = aname, Line = line });
            } else break;
            if (Match(TokenType.Punct, ",")) { Advance(); continue; } break;
        }
        Expect(TokenType.Punct, ")");
        if (Match(TokenType.Punct, ";")) { Advance(); return new ParseFunction { Name = funcName, Line = line, Args = args, Body = null }; }
        var body = ParseBlock();
        return new ParseFunction { Name = funcName, Line = line, Args = args, Body = body };
    }
    private ParseNode ParseBlock() { Expect(TokenType.Punct, "{"); var block = new ParseNode { Kind = NodeKind.NODE_FUNCTION_DEF, Line = _current.Line }; while (!Match(TokenType.Punct, "}") && _current.Type != TokenType.Eof) { if (_current.Type == TokenType.Keyword && IsTypeKeyword(_current.Text)) { var varList = ParseVarDeclList(); if (varList != null) { foreach (var child in varList.Children) block.AddChild(child); continue; } } var stmt = ParseStatement(); if (stmt != null) block.AddChild(stmt); else Advance(); } Expect(TokenType.Punct, "}"); return block; }
    private ParseNode ParseVarDeclList(bool requireSemi = true)
    {
        int line = _current.Line; string typeName = _current.Text; Advance(); while (_current.Text == "*") { typeName += "*"; Advance(); }
        var listNode = new ParseNode { Kind = NodeKind.NODE_VAR_DEF, Line = line, Name = typeName };
        while (true)
        {
            while (_current.Text == "*") { typeName += "*"; Advance(); }
            if (_current.Type != TokenType.Identifier) break;
            string varName = _current.Text; int varLine = _current.Line; Advance();
            int arraySize = 0; bool isArray = false;
            if (Match(TokenType.Punct, "[")) { Advance(); if (_current.Type == TokenType.Number) { arraySize = (int)_current.NumberValue; Advance(); } Expect(TokenType.Punct, "]"); isArray = true; }
            ParseNode initExpr = null; if (_current.Text == "=") { Advance(); initExpr = ParseExpression(); }
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
        if (Match(TokenType.Keyword, "foreach")) return ParseForeach();
        if (Match(TokenType.Keyword, "switch")) return ParseSwitch();
        if (Match(TokenType.Keyword, "case")) return ParseCase();
        if (Match(TokenType.Keyword, "default")) return ParseDefault();
        if (Match(TokenType.Keyword, "if")) { int line = _current.Line; Advance(); Expect(TokenType.Punct, "("); var cond = ParseExpression(); Expect(TokenType.Punct, ")"); var thenStmt = ParseStatementOrBlock(); ParseNode elseStmt = null; if (Match(TokenType.Keyword, "else")) { Advance(); elseStmt = ParseStatementOrBlock(); } var ifNode = new ParseNode { Kind = NodeKind.NODE_IF, Line = line }; ifNode.AddChild(cond); if (thenStmt != null) ifNode.AddChild(thenStmt); if (elseStmt != null) ifNode.AddChild(elseStmt); return ifNode; }
        if (Match(TokenType.Keyword, "while")) { int line = _current.Line; Advance(); Expect(TokenType.Punct, "("); var cond = ParseExpression(); Expect(TokenType.Punct, ")"); var body = ParseStatementOrBlock(); var whileNode = new ParseNode { Kind = NodeKind.NODE_LOOP, Name = "while", Line = line }; whileNode.AddChild(cond); if (body != null) whileNode.AddChild(body); return whileNode; }
        if (Match(TokenType.Keyword, "for")) { int line = _current.Line; Advance(); Expect(TokenType.Punct, "("); ParseNode init = null; if (!Match(TokenType.Punct, ";")) { if (_current.Type == TokenType.Keyword && IsTypeKeyword(_current.Text)) { init = ParseVarDeclList(false); } else { var exprList = new ParseNode { Kind = NodeKind.NODE_FUNCTION_DEF, Line = line }; while (true) { var expr = ParseExpression(); if (expr != null) exprList.AddChild(expr); if (Match(TokenType.Punct, ",")) { Advance(); continue; } break; } init = exprList; } } else { init = new ParseNode { Kind = NodeKind.NODE_NUMBER, Name = "__empty_init", Line = line }; } Expect(TokenType.Punct, ";"); ParseNode cond = null; if (!Match(TokenType.Punct, ";")) { cond = ParseExpression(); } else { cond = new ParseNode { Kind = NodeKind.NODE_NUMBER, Name = "__empty_cond", Line = line }; } Expect(TokenType.Punct, ";"); ParseNode inc = null; if (!Match(TokenType.Punct, ")")) { var incList = new ParseNode { Kind = NodeKind.NODE_FUNCTION_DEF, Line = line }; while (true) { var incExpr = ParseExpression(); if (incExpr != null) incList.AddChild(incExpr); if (Match(TokenType.Punct, ",")) { Advance(); continue; } break; } inc = incList; } else { inc = new ParseNode { Kind = NodeKind.NODE_NUMBER, Name = "__empty_inc", Line = line }; } Expect(TokenType.Punct, ")"); var body = ParseStatementOrBlock(); var forNode = new ParseNode { Kind = NodeKind.NODE_LOOP, Name = "for", Line = line }; forNode.AddChild(init ?? new ParseNode { Kind = NodeKind.NODE_NUMBER, Name = "__empty_init", Line = line }); forNode.AddChild(cond); forNode.AddChild(inc); if (body != null) forNode.AddChild(body); return forNode; }
        if (Match(TokenType.Keyword, "return")) { int line = _current.Line; Advance(); if (Match(TokenType.Punct, ";")) { Advance(); return new ParseNode { Kind = NodeKind.NODE_RETURN, Line = line }; } var expr = ParseExpression(); Expect(TokenType.Punct, ";"); var ret = new ParseNode { Kind = NodeKind.NODE_RETURN, Line = line }; if (expr != null) ret.AddChild(expr); return ret; }
        if (Match(TokenType.Keyword, "break")) { int line = _current.Line; Advance(); if (Match(TokenType.Punct, ";")) Advance(); return new ParseNode { Kind = NodeKind.NODE_LOOP, Name = "break", Line = line }; }
        if (Match(TokenType.Keyword, "continue")) { int line = _current.Line; Advance(); if (Match(TokenType.Punct, ";")) Advance(); return new ParseNode { Kind = NodeKind.NODE_LOOP, Name = "continue", Line = line }; }
        if (Match(TokenType.Punct, "{")) { return ParseBlock(); }
        if (Match(TokenType.Punct, ";")) { Advance(); return null; }
        if (_current.Type == TokenType.Keyword && IsTypeKeyword(_current.Text)) { return ParseVarDeclList(); }
        var exprStmt2 = ParseExpression(); if (Match(TokenType.Punct, ";")) Advance(); if (exprStmt2 != null) { var wrapper = new ParseNode { Kind = NodeKind.NODE_FUNCTION_DEF, Line = exprStmt2.Line }; wrapper.AddChild(exprStmt2); return wrapper; } return null;
    }

    private ParseNode ParseSwitch()
    {
        int line = _current.Line;
        Advance(); // switch
        Expect(TokenType.Punct, "(");
        var expr = ParseExpression();
        Expect(TokenType.Punct, ")");
        Expect(TokenType.Punct, "{");
        var switchNode = new ParseNode { Kind = NodeKind.NODE_LOOP, Name = "switch", Line = line };
        switchNode.AddChild(expr);
        var casesBlock = new ParseNode { Kind = NodeKind.NODE_FUNCTION_DEF, Line = line };
        while (!Match(TokenType.Punct, "}") && _current.Type != TokenType.Eof)
        {
            if (Match(TokenType.Keyword, "case"))
            {
                casesBlock.AddChild(ParseCase());
            }
            else if (Match(TokenType.Keyword, "default"))
            {
                casesBlock.AddChild(ParseDefault());
            }
            else
            {
                var stmt = ParseStatement();
                if (stmt != null) casesBlock.AddChild(stmt);
                else Advance();
            }
        }
        Expect(TokenType.Punct, "}");
        switchNode.AddChild(casesBlock);
        return switchNode;
    }
    private ParseNode ParseCase()
    {
        int line = _current.Line;
        Expect(TokenType.Keyword, "case");
        var val = ParseExpression();
        Expect(TokenType.Punct, ":");
        var caseNode = new ParseNode { Kind = NodeKind.NODE_LOOP, Name = "case", Line = line };
        caseNode.AddChild(val);
        // collect statements until next case/default/}
        var body = new ParseNode { Kind = NodeKind.NODE_FUNCTION_DEF, Line = line };
        while (!Match(TokenType.Punct, "}") && !Match(TokenType.Keyword, "case") && !Match(TokenType.Keyword, "default") && _current.Type != TokenType.Eof)
        {
            var stmt = ParseStatement();
            if (stmt != null) body.AddChild(stmt);
            else break;
        }
        caseNode.AddChild(body);
        return caseNode;
    }
    private ParseNode ParseDefault()
    {
        int line = _current.Line;
        Expect(TokenType.Keyword, "default");
        Expect(TokenType.Punct, ":");
        var defNode = new ParseNode { Kind = NodeKind.NODE_LOOP, Name = "default", Line = line };
        var body = new ParseNode { Kind = NodeKind.NODE_FUNCTION_DEF, Line = line };
        while (!Match(TokenType.Punct, "}") && !Match(TokenType.Keyword, "case") && !Match(TokenType.Keyword, "default") && _current.Type != TokenType.Eof)
        {
            var stmt = ParseStatement();
            if (stmt != null) body.AddChild(stmt);
            else break;
        }
        defNode.AddChild(body);
        return defNode;
    }

    private ParseNode ParseForeach() { int line = _current.Line; Advance(); Expect(TokenType.Punct, "(");
        string varName1 = null; string varName2 = null;
        // helper to parse possibly typed identifier
        void SkipType() { if (_current.Type == TokenType.Keyword && IsTypeKeyword(_current.Text)) { Advance(); while (_current.Text == "*") Advance(); } }
        SkipType();
        if (_current.Type == TokenType.Identifier) { varName1 = _current.Text; Advance(); if (Match(TokenType.Punct, ",") || MatchText(",")) { Advance(); SkipType(); if (_current.Type == TokenType.Identifier) { varName2 = _current.Text; Advance(); } } }
        // if first token was type and we have two vars with comma, second var already handled, but also handle case where first var was type+name and second var typed
        if (Match(TokenType.Keyword, "in") || (Match(TokenType.Identifier, "in"))) { Advance(); }
        var container = ParseExpression(); Expect(TokenType.Punct, ")"); var body = ParseStatementOrBlock();
        var foreachNode = new ParseNode { Kind = NodeKind.NODE_LOOP, Name = "foreach", Line = line };
        if (varName1 != null) { var keyNode = ParseNode.NewIdentifier(varName1, line); foreachNode.AddChild(keyNode); }
        if (varName2 != null) { var valNode = ParseNode.NewIdentifier(varName2, line); foreachNode.AddChild(valNode); }
        if (varName1 == null && varName2 == null) { var keyNode = ParseNode.NewIdentifier("__foreach_key", line); foreachNode.AddChild(keyNode); }
        foreachNode.AddChild(container); if (body != null) foreachNode.AddChild(body); return foreachNode; }
    private ParseNode ParseStatementOrBlock() { if (Match(TokenType.Punct, "{")) return ParseBlock(); return ParseStatement(); }
    private ParseNode ParseExpression(int minPrec = 0) { var left = ParseUnary(); if (left == null) return null; while (true) { string op = _current.Text; int prec = GetPrecedence(op); if (prec < minPrec || prec < 0) break; bool isRightAssoc = (op == "=" || op.EndsWith("=")); int nextMinPrec = isRightAssoc ? prec : prec + 1; Advance(); var right = ParseExpression(nextMinPrec); if (right == null) break; if (op == "=") { var assign = new ParseNode { Kind = NodeKind.NODE_ASSIGN, Line = left.Line, Left = left, Right = right }; assign.AddChild(left); assign.AddChild(right); left = assign; } else { var binOp = new ParseNode { Kind = NodeKind.NODE_BINARY_OP, Name = op, Line = left.Line, Left = left, Right = right }; binOp.AddChild(left); binOp.AddChild(right); left = binOp; } }
        // ternary ?: precedence is above assignment (0) and below || (1),
        // so only consume it when minPrec <= 0; otherwise leave it for the outer call.
        if (minPrec <= 0 && MatchText("?"))
        {
            int tline = left.Line;
            Advance();
            var trueExpr = ParseExpression();
            Expect(TokenType.Punct, ":");
            var falseExpr = ParseExpression(minPrec);
            var tern = new ParseNode { Kind = NodeKind.NODE_TERNARY, Line = tline };
            tern.AddChild(left);
            if (trueExpr != null) tern.AddChild(trueExpr);
            if (falseExpr != null) tern.AddChild(falseExpr);
            left = tern;
        }
        return left; }
    private ParseNode ParseUnary() {
        if (Match(TokenType.Operator, "!") || Match(TokenType.Punct, "!") || MatchText("!") || Match(TokenType.Operator, "~") || MatchText("~")) { int line = _current.Line; string op = _current.Text; Advance(); var operand = ParseUnary(); if (operand != null) { var node = new ParseNode { Kind = NodeKind.NODE_BINARY_OP, Name = op, Line = line }; node.AddChild(operand); return TryParseIndex(node); } }
        if (Match(TokenType.Operator, "-") || Match(TokenType.Punct, "-") || MatchText("-")) { int line = _current.Line; Advance(); var operand = ParseUnary(); if (operand != null) { var zero = ParseNode.NewNumber(0, line); var node = new ParseNode { Kind = NodeKind.NODE_BINARY_OP, Name = "-", Line = line }; node.AddChild(zero); node.AddChild(operand); return TryParseIndex(node); } }
        if (Match(TokenType.Operator, "+") || Match(TokenType.Punct, "+") || MatchText("+")) { int line = _current.Line; Advance(); var operand = ParseUnary(); return operand != null ? TryParseIndex(operand) : null; }
        if (MatchText("++") || MatchText("--")) { int line = _current.Line; string op = _current.Text; Advance(); var operand = ParseUnary(); if (operand != null) { var node = new ParseNode { Kind = NodeKind.NODE_BINARY_OP, Name = op, Line = line }; node.AddChild(operand); return TryParseIndex(node); } }
        var primary = ParsePrimary();
        if (primary == null) return null;
        if (primary.Kind == NodeKind.NODE_IDENTIFIER) { if (MatchText("++") || MatchText("--")) { int line = _current.Line; string op = _current.Text == "++" ? "post++" : "post--"; Advance(); var node = new ParseNode { Kind = NodeKind.NODE_BINARY_OP, Name = op, Line = line }; node.AddChild(primary); node.Left = primary; return node; } }
        return primary;
    }
    private bool IsBinaryOp(string op) { switch (op) { case "*": case "/": case "%": case "+": case "-": case "<<": case ">>": case "<": case "<=": case ">": case ">=": case "==": case "!=": case "&": case "^": case "|": case "&&": case "||": case "=": case "+=": case "-=": case "*=": case "/=": case "%=": case "&=": case "|=": case "^=": case "<<=": case ">>=": return true; default: return false; } }
    private int GetPrecedence(string op) { switch (op) { case "*": case "/": case "%": return 10; case "+": case "-": return 9; case "<<": case ">>": return 8; case "<": case "<=": case ">": case ">=": return 7; case "==": case "!=": return 6; case "&": return 5; case "^": return 4; case "|": return 3; case "&&": return 2; case "||": return 1; case "=": case "+=": case "-=": case "*=": case "/=": case "%=": case "&=": case "|=": case "^=": case "<<=": case ">>=": return 0; default: return -1; } }
        private ParseNode ParsePrimary()
    {
        // catch(expr) 1:1 Neolith
        if (_current.Text == "catch")
        {
            int line = _current.Line;
            Advance();
            if (Match(TokenType.Punct, "("))
            {
                Advance();
                var inner = ParseExpression();
                Expect(TokenType.Punct, ")");
                var catchNode = new ParseNode { Kind = NodeKind.NODE_CATCH, Line = line };
                if (inner != null) catchNode.AddChild(inner);
                return TryParseIndex(catchNode);
            }
            // catch without parens? treat as identifier fallback
        }
        // new(class) / new(path) -> clone_object alias
        if (_current.Text == "new")
        {
            int line = _current.Line;
            Advance();
            var args = new List<ParseNode>();
            if (Match(TokenType.Punct, "("))
            {
                Advance();
                while (!Match(TokenType.Punct, ")") && _current.Type != TokenType.Eof)
                {
                    var arg = ParseExpression();
                    if (arg != null) args.Add(arg);
                    if (Match(TokenType.Punct, ",")) Advance(); else break;
                }
                Expect(TokenType.Punct, ")");
            }
            var newNode = new ParseNode { Kind = NodeKind.NODE_NEW, Line = line };
            foreach (var a in args) newNode.AddChild(a);
            return TryParseIndex(newNode);
        }
        if (Match(TokenType.Punct, "::"))
        {
            Advance();
            if (_current.Type == TokenType.Identifier)
            {
                string mname = _current.Text;
                int line = _current.Line;
                Advance();
                var args = new List<ParseNode>();
                if (Match(TokenType.Punct, "("))
                {
                    Advance();
                    while (!Match(TokenType.Punct, ")") && _current.Type != TokenType.Eof)
                    {
                        var arg = ParseExpression();
                        if (arg != null) args.Add(arg);
                        if (Match(TokenType.Punct, ",")) Advance(); else break;
                    }
                    Expect(TokenType.Punct, ")");
                }
                var wrapper = new ParseNode { Kind = NodeKind.NODE_CALL, Name = "::" + mname, Line = line };
                wrapper.AddChild(ParseNode.NewIdentifier(mname, line));
                foreach (var a in args) wrapper.AddChild(a);
                return wrapper;
            }
        }
        if (Match(TokenType.Punct, "(:")) { return ParseClosure(); }
        if (Match(TokenType.Punct, "("))
        {
            // cast detection: (type) or (type*) expr
            int saveLine = _current.Line;
            // peek via reflection is expensive, do simple lookahead using lexer state copy
            // We implement by saving token and trying to parse type
            var savedCurrent = _current;
            var savedPrev = _prev;
            var lexerPosField = _lexer.GetType().GetField("_pos", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var lexerLineField = _lexer.GetType().GetField("_line", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var lexerColField = _lexer.GetType().GetField("_col", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            int savedPos = lexerPosField != null ? (int)lexerPosField.GetValue(_lexer) : 0;
            int savedL = lexerLineField != null ? (int)lexerLineField.GetValue(_lexer) : 0;
            int savedC = lexerColField != null ? (int)lexerColField.GetValue(_lexer) : 0;

            // try cast
            Advance(); // consume '('
            bool isCast = false;
            if (_current.Type == TokenType.Keyword && IsTypeKeyword(_current.Text))
            {
                Advance();
                while (_current.Text == "*") Advance();
                if (Match(TokenType.Punct, ")"))
                {
                    isCast = true;
                    Advance(); // consume ')'
                }
            }
            if (isCast)
            {
                // cast found, parse operand as unary and return it (ignore cast)
                var operand = ParseUnary();
                return operand != null ? TryParseIndex(operand) : null;
            }
            else
            {
                // not cast, restore lexer and token
                if (lexerPosField != null) lexerPosField.SetValue(_lexer, savedPos);
                if (lexerLineField != null) lexerLineField.SetValue(_lexer, savedL);
                if (lexerColField != null) lexerColField.SetValue(_lexer, savedC);
                _current = savedCurrent;
                _prev = savedPrev;
            }

            try {
                var f = _lexer.GetType().GetField("_src", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var p = _lexer.GetType().GetField("_pos", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f!=null && p!=null) {
                    string src = (string)f.GetValue(_lexer);
                    int pos = (int)p.GetValue(_lexer);
                    int pp = pos;
                    while (pp < src.Length && char.IsWhiteSpace(src[pp])) pp++;
                    if (pp < src.Length && src[pp]=='{' ) { int line = _current.Line; Advance(); Expect(TokenType.Punct, "{"); var arrNode = new ParseNode { Kind = NodeKind.NODE_ARRAY, Name = "array_lit", Line = line }; while (!Match(TokenType.Punct, "}") && _current.Type != TokenType.Eof) { var elem = ParseExpression(); if (elem != null) arrNode.AddChild(elem); if (Match(TokenType.Punct, ",")) { Advance(); continue; } break; } Expect(TokenType.Punct, "}"); Expect(TokenType.Punct, ")"); return TryParseIndex(arrNode); }
                    if (pp < src.Length && src[pp]=='[') { int line = _current.Line; Advance(); Expect(TokenType.Punct, "["); var mapNode = new ParseNode { Kind = NodeKind.NODE_MAPPING, Line = line }; while (!Match(TokenType.Punct, "]") && _current.Type != TokenType.Eof) { var key = ParseExpression(); if (Match(TokenType.Punct, ":") || Match(TokenType.Operator, ":")) Advance(); else { _errors.Add($"{_fileName}:{_current.Line}: expected : in mapping"); break; } var val = ParseExpression(); if (key != null) mapNode.AddChild(key); if (val != null) mapNode.AddChild(val); if (Match(TokenType.Punct, ",")) { Advance(); continue; } break; } Expect(TokenType.Punct, "]"); Expect(TokenType.Punct, ")"); return TryParseIndex(mapNode); }
                }
            } catch {}
            Advance(); var expr = ParseExpression(); Expect(TokenType.Punct, ")"); return TryParseIndex(expr);
        }
        if (_current.Type == TokenType.Number) { var node = ParseNode.NewNumber(_current.NumberValue, _current.Line); Advance(); return TryParseIndex(node); }
        if (_current.Type == TokenType.Real) { var node = ParseNode.NewReal(_current.RealValue, _current.Line); Advance(); return TryParseIndex(node); }
        if (_current.Type == TokenType.String) { var s = _current.Text ?? ""; var node = ParseNode.NewString(s, s.Length, _current.Line); Advance(); return TryParseIndex(node); }
        if (_current.Type == TokenType.Identifier || (_current.Type == TokenType.Keyword && IsEfunKeyword(_current.Text)))
        {
            var baseNode = ParseNode.NewIdentifier(_current.Text, _current.Line); Advance();
            baseNode = TryParseIndex(baseNode);
            ParseNode current = baseNode;
            while (true)
            {
                if (Match(TokenType.Punct, "("))
                {
                    Advance();
                    var args = new List<ParseNode>();
                    while (!Match(TokenType.Punct, ")") && _current.Type != TokenType.Eof)
                    {
                        var arg = ParseExpression();
                        if (arg != null) args.Add(arg);
                        if (Match(TokenType.Punct, ",")) Advance(); else break;
                    }
                    Expect(TokenType.Punct, ")");
                    var callName = (current.Kind == NodeKind.NODE_IDENTIFIER) ? current.Name : "";
                    var call = new ParseNode { Kind = NodeKind.NODE_CALL, Name = callName, Line = current.Line };
                    if (current.Kind == NodeKind.NODE_IDENTIFIER)
                    {
                        // function call
                    }
                    else
                    {
                        call.AddChild(current);
                    }
                    foreach (var a in args) call.AddChild(a);
                    // preserve identifier name for efuns
                    if (!string.IsNullOrEmpty(callName) && current.Kind == NodeKind.NODE_IDENTIFIER) call.Name = callName;
                    else if (current.Kind != NodeKind.NODE_IDENTIFIER) { /* keep as dot chain */ }
                    current = TryParseIndex(call);
                    continue;
                }
                if (_current.Text == "->")
                {
                    Advance();
                    if (_current.Type != TokenType.Identifier) { _errors.Add($"{_fileName}:{_current.Line}: expected method after ->"); return current; }
                    string method = _current.Text; Advance();
                    var args = new List<ParseNode>();
                    if (Match(TokenType.Punct, "("))
                    {
                        Advance();
                        while (!Match(TokenType.Punct, ")") && _current.Type != TokenType.Eof)
                        {
                            var arg = ParseExpression();
                            if (arg != null) args.Add(arg);
                            if (Match(TokenType.Punct, ",")) Advance(); else break;
                        }
                        Expect(TokenType.Punct, ")");
                    }
                    var dot = ParseNode.NewDotCall(current, method, args, current.Line);
                    current = TryParseIndex(dot);
                    continue;
                }
                break;
            }
            return current;
        }
        if (_current.Type == TokenType.Keyword && (_current.Text == "sizeof" || _current.Text == "evaluate" || _current.Text == "filter" || _current.Text == "map" || _current.Text == "sort_array" || _current.Text == "clone_object" || _current.Text == "typeof")) { string name = _current.Text; int line = _current.Line; Advance(); if (Match(TokenType.Punct, "(")) { Advance(); var args = new List<ParseNode>(); while (!Match(TokenType.Punct, ")") && _current.Type != TokenType.Eof) { var arg = ParseExpression(); if (arg != null) args.Add(arg); if (Match(TokenType.Punct, ",")) Advance(); else break; } Expect(TokenType.Punct, ")"); var call = new ParseNode { Kind = NodeKind.NODE_CALL, Name = name, Line = line }; foreach (var a in args) call.AddChild(a); return TryParseIndex(call); } var id = ParseNode.NewIdentifier(name, line); return TryParseIndex(id); }
        if (Match(TokenType.Punct, "(")) { Advance(); var expr = ParseExpression(); Expect(TokenType.Punct, ")"); return TryParseIndex(expr); }
        return null;
    }
    private ParseNode ParseClosure() { Expect(TokenType.Punct, "(:"); var closureNode = new ParseNode { Kind = NodeKind.NODE_CLOSURE, Line = _prev.Line }; if (!Match(TokenType.Punct, ":)")) { while (true) { var expr = ParseExpression(); if (expr != null) closureNode.AddChild(expr); if (Match(TokenType.Punct, ",")) { Advance(); continue; } break; } } Expect(TokenType.Punct, ":)"); return closureNode; }
    private ParseNode TryParseIndex(ParseNode baseNode)
    {
        var current = baseNode;
        while (true)
        {
            if (Match(TokenType.Punct, "["))
            {
                int line = _current.Line;
                Advance();
                ParseNode startNode = null;
                ParseNode endNode = null;
                bool startReverse = false;
                bool endReverse = false;
                bool endExclusive = false;
                bool isRange = false;

                // handle [..end] case: starts with ..
                if (Match(TokenType.Punct, ".."))
                {
                    isRange = true;
                    Advance();
                    // after .., optional < for reverse end (e.g. ..<0)
                    if (Match(TokenType.Operator, "<"))
                    {
                        // check if this < is exclusive marker or reverse?
                        // We treat < as reverse marker. For ..<0, it's reverse 0.
                        // For exclusive syntax ..<2, it's also reverse? We treat as reverse.
                        endReverse = true;
                        Advance();
                    }
                    if (!Match(TokenType.Punct, "]"))
                    {
                        endNode = ParseExpression();
                    }
                }
                else
                {
                    // optional < for start reverse
                    if (Match(TokenType.Operator, "<"))
                    {
                        startReverse = true;
                        Advance();
                    }
                    if (!Match(TokenType.Punct, "..") && !Match(TokenType.Punct, "]"))
                    {
                        startNode = ParseExpression();
                    }
                    if (Match(TokenType.Punct, ".."))
                    {
                        isRange = true;
                        Advance();
                        // after .., optional < for end reverse (handles ..<2)
                        if (Match(TokenType.Operator, "<"))
                        {
                            endReverse = true;
                            Advance();
                        }
                        if (!Match(TokenType.Punct, "]"))
                        {
                            endNode = ParseExpression();
                        }
                    }
                }

                Expect(TokenType.Punct, "]");
                ParseNode result;
                if (isRange)
                {
                    result = new ParseNode { Kind = NodeKind.NODE_RANGE, Line = line };
                    // encode flags: startReverse, endReverse, exclusive (0 for now, we ignore exclusive and treat as inclusive with clamp)
                    result.Name = $"{(startReverse?1:0)}{(endReverse?1:0)}0{(endExclusive?1:0)}";
                    result.Children.Add(current);
                    result.Children.Add(startNode);
                    result.Children.Add(endNode);
                    result.Left = current;
                    result.Right = startNode;
                }
                else
                {
                    if (startReverse)
                    {
                        result = new ParseNode { Kind = NodeKind.NODE_RINDEX, Line = line };
                        result.Children.Add(current);
                        if (startNode != null) result.Children.Add(startNode);
                        else result.Children.Add(ParseNode.NewNumber(0, line));
                        result.Left = current;
                        result.Right = startNode;
                    }
                    else
                    {
                        result = new ParseNode { Kind = NodeKind.NODE_ARRAY, Name = "[]", Line = line, Left = current, Right = startNode };
                        result.Children.Add(current);
                        if (startNode != null) result.Children.Add(startNode);
                    }
                }
                current = result;
                // after indexing, also handle .member and .method() chains (e.g. args["options"].len())
                while (true)
                {
                    if (_current.Text == "->" || _current.Text == ".")
                    {
                        string arrow = _current.Text;
                        Advance();
                        if (_current.Type != TokenType.Identifier)
                        {
                            _errors.Add($"{_fileName}:{_current.Line}: expected method after {arrow}");
                            break;
                        }
                        string method = _current.Text;
                        Advance();
                        var args = new List<ParseNode>();
                        if (Match(TokenType.Punct, "("))
                        {
                            Advance();
                            while (!Match(TokenType.Punct, ")") && _current.Type != TokenType.Eof)
                            {
                                var arg = ParseExpression();
                                if (arg != null) args.Add(arg);
                                if (Match(TokenType.Punct, ",")) Advance(); else break;
                            }
                            Expect(TokenType.Punct, ")");
                        }
                        else
                        {
                            // property access without parens, treat as field access? For now keep as dot call with no args
                            // but we will generate as dot call; if no parens, it's still a member access
                            // To handle .len() we need parens, so this branch is for .len without parens? Actually len() has parens
                            // For property like .len without parens? We'll still create dot call with empty args and handle in codegen as property
                        }
                        var dot = ParseNode.NewDotCall(current, method, args, current.Line);
                        current = dot;
                        continue;
                    }
                    break;
                }
                continue;
            }
            if (_current.Text == "->" || _current.Text == ".")
            {
                string arrow = _current.Text;
                Advance();
                if (_current.Type != TokenType.Identifier)
                {
                    _errors.Add($"{_fileName}:{_current.Line}: expected method after {arrow}");
                    return current;
                }
                string method = _current.Text;
                Advance();
                var args = new List<ParseNode>();
                if (Match(TokenType.Punct, "("))
                {
                    Advance();
                    while (!Match(TokenType.Punct, ")") && _current.Type != TokenType.Eof)
                    {
                        var arg = ParseExpression();
                        if (arg != null) args.Add(arg);
                        if (Match(TokenType.Punct, ",")) Advance(); else break;
                    }
                    Expect(TokenType.Punct, ")");
                }
                var dot = ParseNode.NewDotCall(current, method, args, current.Line);
                current = dot;
                continue;
            }
            break;
        }
        return current;
    }
}

