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

        public List<AstNode> Parse() { var nodes = new List<AstNode>(); while (!Check(TokenType.EOF)) nodes.Add(ParseTopLevel()); return nodes; }

        private AstNode ParseTopLevel() {
            if (!IsTypeKeyword()) throw new Exception($"[Parser] Line {Current.Line}: Unexpected {Current.Type}");
            if (_pos + 2 < _tokens.Count && _tokens[_pos + 1].Type == TokenType.Identifier && _tokens[_pos + 2].Type == TokenType.LeftParen)
                return ParseFunctionDeclaration();
            return ParseVariableDeclaration();
        }

        private AstNode ParseVariableDeclaration() {
            string tName = Consume().Value; string vName = Consume().Value; AstNode init = null;
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
            
            if (IsTypeKeyword() && _pos + 2 < _tokens.Count && _tokens[_pos + 1].Type == TokenType.Identifier && _tokens[_pos + 2].Type != TokenType.LeftParen) 
                return ParseVariableDeclaration();
                
            // 【核心修復】解析賦值語句: identifier = expression;
            if (Check(TokenType.Identifier) && _pos + 1 < _tokens.Count && _tokens[_pos + 1].Type == TokenType.Assign) {
                string name = Consume().Value;
                Consume(); // eat '='
                var val = ParseExpression();
                Expect(TokenType.Semicolon);
                return new AssignmentNode { VariableName = name, Value = val };
            }

            var expr = ParseExpression(); Expect(TokenType.Semicolon); return expr;
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
            if (Check(TokenType.IntLiteral)) return new LiteralNode { Value = LpcValue.Create(int.Parse(Consume().Value)) };
            if (Check(TokenType.StringLiteral)) return new LiteralNode { Value = LpcValue.Create(Consume().Value) };
            if (Check(TokenType.Identifier)) {
                string name = Consume().Value;
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
            throw new Exception($"[Parser] Line {Current.Line}: Cannot parse {Current.Type}");
        }
    }
}
