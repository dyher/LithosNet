#nullable disable
using System;
using System.Collections.Generic;
using LithosNet.Core;

namespace LithosNet.Compiler {
    public class Parser {
        private readonly List<Token> _tokens;
        private int _pos;
        public Parser(List<Token> tokens) { _tokens = tokens; _pos = 0; }
        private Token Current => _pos < _tokens.Count ? _tokens[_pos] : new Token(TokenType.EOF, "");
        private Token Consume() => _tokens[_pos++];
        private bool Check(TokenType t) => Current.Type == t;
        private bool IsTypeKeyword() => Check(TokenType.Keyword_Int) || Check(TokenType.Keyword_String) || Check(TokenType.Keyword_Void);
        private void Expect(TokenType type) { if (Current.Type != type) throw new Exception($"[Parser] Line {Current.Line}: Expected {type}, got {Current.Type}"); Consume(); }

        // 【核心前瞻函數】精準判斷接下來是否為變數宣告 (支援 int x 與 int[] x)
        private bool IsVariableDeclaration() {
            if (!IsTypeKeyword()) return false;
            // 情況 1: int x = ...
            if (_pos + 1 < _tokens.Count && _tokens[_pos + 1].Type == TokenType.Identifier) return true;
            // 情況 2: int[] x = ...
            if (_pos + 3 < _tokens.Count && 
                _tokens[_pos + 1].Type == TokenType.LeftBracket && 
                _tokens[_pos + 2].Type == TokenType.RightBracket && 
                _tokens[_pos + 3].Type == TokenType.Identifier) return true;
            return false;
        }

        public List<AstNode> Parse() { var nodes = new List<AstNode>(); while (!Check(TokenType.EOF)) nodes.Add(ParseTopLevel()); return nodes; }

        private AstNode ParseTopLevel() {
            if (!IsTypeKeyword()) throw new Exception($"[Parser] Line {Current.Line}: Unexpected {Current.Type}");
            // 判斷是否為函數宣告 (例如: int main())
            if (_pos + 2 < _tokens.Count && _tokens[_pos + 1].Type == TokenType.Identifier && _tokens[_pos + 2].Type == TokenType.LeftParen)
                return ParseFunctionDeclaration();
            return ParseVariableDeclaration();
        }

        private AstNode ParseVariableDeclaration() {
            string tName = Consume().Value; // 吃掉 int 或 string
            
            // 處理陣列類型宣告 int[] 或 string[]
            if (Check(TokenType.LeftBracket) && _pos + 1 < _tokens.Count && _tokens[_pos + 1].Type == TokenType.RightBracket) {
                Consume(); // eat '['
                Consume(); // eat ']'
                tName += "[]"; 
            }

            string vName = Consume().Value; 
            AstNode init = null;
            if (Check(TokenType.Assign)) { Consume(); init = ParseExpression(); }
            Expect(TokenType.Semicolon);
            return new VariableDeclarationNode { TypeName = tName, VariableName = vName, Initializer = init };
        }

        private AstNode ParseFunctionDeclaration() {
            string ret = Consume().Value; string name = Consume().Value; Expect(TokenType.LeftParen);
            var parms = new List<ParameterNode>();
            while (!Check(TokenType.RightParen)) { parms.Add(new ParameterNode { TypeName = Consume().Value, Name = Consume().Value }); if (Check(TokenType.Comma)) Consume(); }
            Expect(TokenType.RightParen); Expect(TokenType.LeftBrace);
            var body = new List<AstNode>();
            while (!Check(TokenType.RightBrace) && !Check(TokenType.EOF)) body.Add(ParseStatement());
            Expect(TokenType.RightBrace);
            return new FunctionDeclarationNode { ReturnType = ret, Name = name, Parameters = parms, Body = body };
        }

        private AstNode ParseStatement() {
            if (Check(TokenType.Keyword_Return)) { Consume(); var v = ParseExpression(); Expect(TokenType.Semicolon); return new ReturnNode { Value = v }; }
            if (Check(TokenType.Keyword_If)) return ParseIfStatement();
            if (Check(TokenType.Keyword_While)) return ParseWhileStatement();
            
            // 【完美修復】使用前瞻函數判斷是否為變數宣告
            if (IsVariableDeclaration()) {
                return ParseVariableDeclaration();
            }
                
            // 處理賦值與陣列索引賦值
            if (Check(TokenType.Identifier)) {
                var expr = ParseExpression();
                if (expr is IndexAccessNode idx && Check(TokenType.Assign)) {
                    Consume(); var val = ParseExpression(); Expect(TokenType.Semicolon);
                    return new IndexAssignmentNode { Array = idx.Array, Index = idx.Index, Value = val };
                }
                if (expr is VariableRefNode vref && Check(TokenType.Assign)) {
                    Consume(); var val = ParseExpression(); Expect(TokenType.Semicolon);
                    return new AssignmentNode { VariableName = vref.Name, Value = val };
                }
                Expect(TokenType.Semicolon);
                return expr;
            }

            var e = ParseExpression(); Expect(TokenType.Semicolon); return e;
        }

        private AstNode ParseIfStatement() {
            Consume(); Expect(TokenType.LeftParen); var cond = ParseExpression(); Expect(TokenType.RightParen);
            var thenBranch = ParseBlockOrStatement(); AstNode elseBranch = null;
            if (Check(TokenType.Keyword_Else)) { Consume(); elseBranch = ParseBlockOrStatement(); }
            return new IfNode { Condition = cond, ThenBranch = thenBranch, ElseBranch = elseBranch };
        }

        private AstNode ParseWhileStatement() {
            Consume(); Expect(TokenType.LeftParen); var cond = ParseExpression(); Expect(TokenType.RightParen);
            var body = ParseBlockOrStatement();
            return new WhileNode { Condition = cond, Body = body };
        }

        private AstNode ParseBlockOrStatement() {
            if (Check(TokenType.LeftBrace)) {
                Consume(); var stmts = new List<AstNode>();
                while (!Check(TokenType.RightBrace) && !Check(TokenType.EOF)) stmts.Add(ParseStatement());
                Expect(TokenType.RightBrace);
                return new BlockNode { Statements = stmts };
            }
            return ParseStatement();
        }

        private AstNode ParseExpression() {
            var left = ParseTerm();
            while (Check(TokenType.Equal) || Check(TokenType.NotEqual) || 
                   Check(TokenType.Less) || Check(TokenType.Greater) ||
                   Check(TokenType.LessEqual) || Check(TokenType.GreaterEqual)) {
                string op = Consume().Value;
                var right = ParseTerm();
                left = new BinaryOpNode { Left = left, Op = op, Right = right };
            }
            return left;
        }

        private AstNode ParseTerm() {
            var left = ParsePrimary();
            while (Check(TokenType.Plus) || Check(TokenType.Minus)) {
                string op = Consume().Value;
                var right = ParsePrimary();
                left = new BinaryOpNode { Left = left, Op = op, Right = right };
            }
            return left;
        }

        private AstNode ParsePrimary() {
            if (Check(TokenType.LeftBracket)) {
                Consume();
                var elements = new List<AstNode>();
                while (!Check(TokenType.RightBracket)) {
                    elements.Add(ParseExpression());
                    if (Check(TokenType.Comma)) Consume();
                }
                Expect(TokenType.RightBracket);
                return new ArrayLiteralNode { Elements = elements };
            }

            if (Check(TokenType.IntLiteral)) return new LiteralNode { Value = LpcValue.Create(int.Parse(Consume().Value)) };
            if (Check(TokenType.StringLiteral)) return new LiteralNode { Value = LpcValue.Create(Consume().Value) };
            
            if (Check(TokenType.Identifier)) {
                string name = Consume().Value;
                if (Check(TokenType.LeftBracket)) {
                    Consume(); var idx = ParseExpression(); Expect(TokenType.RightBracket);
                    return new IndexAccessNode { Array = new VariableRefNode { Name = name }, Index = idx };
                }
                if (Check(TokenType.LeftParen)) {
                    Consume(); var args = new List<AstNode>();
                    while (!Check(TokenType.RightParen)) { args.Add(ParseExpression()); if (Check(TokenType.Comma)) Consume(); }
                    Expect(TokenType.RightParen); return new FunctionCallNode { Name = name, Arguments = args };
                }
                return new VariableRefNode { Name = name };
            }
            if (Check(TokenType.LeftParen)) {
                Consume(); var expr = ParseExpression(); Expect(TokenType.RightParen); return expr;
            }
            throw new Exception($"[Parser] Line {Current.Line}: Cannot parse {Current.Type} ('{Current.Value}')");
        }
    }
}
