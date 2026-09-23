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

        private bool IsVariableDeclaration() {
            if (!IsTypeKeyword()) return false;
            if (_pos + 1 < _tokens.Count && _tokens[_pos + 1].Type == TokenType.Identifier) return true;
            if (_pos + 3 < _tokens.Count && 
                _tokens[_pos + 1].Type == TokenType.LeftBracket && 
                _tokens[_pos + 2].Type == TokenType.RightBracket && 
                _tokens[_pos + 3].Type == TokenType.Identifier) return true;
            return false;
        }

        public List<AstNode> Parse() { var nodes = new List<AstNode>(); while (!Check(TokenType.EOF)) nodes.Add(ParseTopLevel()); return nodes; }

        private AstNode ParseTopLevel() {
            if (!IsTypeKeyword()) throw new Exception($"[Parser] Line {Current.Line}: Unexpected {Current.Type}");
            if (_pos + 2 < _tokens.Count && _tokens[_pos + 1].Type == TokenType.Identifier && _tokens[_pos + 2].Type == TokenType.LeftParen)
                return ParseFunctionDeclaration();
            return ParseVariableDeclaration();
        }

        private AstNode ParseVariableDeclaration() {
            string tName = Consume().Value;
            if (Check(TokenType.LeftBracket) && _pos + 1 < _tokens.Count && _tokens[_pos + 1].Type == TokenType.RightBracket) {
                Consume(); Consume(); tName += "[]";
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
            while (!Check(TokenType.RightParen)) { 
                string pType = Consume().Value;
                if (Check(TokenType.LeftBracket)) { Consume(); Consume(); pType += "[]"; }
                parms.Add(new ParameterNode { TypeName = pType, Name = Consume().Value }); 
                if (Check(TokenType.Comma)) Consume(); 
            }
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
            if (Check(TokenType.Keyword_For)) return ParseForStatement();
            
            if (IsVariableDeclaration()) return ParseVariableDeclaration();
                
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
            return new WhileNode { Condition = cond, Body = ParseBlockOrStatement() };
        }

        // 【新增】for (init; cond; step) { body }
        private AstNode ParseForStatement() {
            Consume(); // eat 'for'
            Expect(TokenType.LeftParen);
            
            // init: int i = 0; 或 i = 0;
            AstNode init = null;
            if (!Check(TokenType.Semicolon)) {
                if (IsVariableDeclaration()) init = ParseVariableDeclaration(); // 這會自己吃掉 ;
                else { init = ParseExpression(); Expect(TokenType.Semicolon); }
            } else {
                Consume(); // eat ';'
            }
            
            // condition: i < 10
            AstNode cond = null;
            if (!Check(TokenType.Semicolon)) cond = ParseExpression();
            Expect(TokenType.Semicolon);
            
            // step: i = i + 1
            AstNode step = null;
            if (!Check(TokenType.RightParen)) {
                step = ParseExpression();
            }
            Expect(TokenType.RightParen);
            
            return new ForNode { Init = init, Condition = cond, Step = step, Body = ParseBlockOrStatement() };
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

        // 表達式優先級: 比較 < 加減 < 乘除
        private AstNode ParseExpression() {
            var left = ParseAdditive();
            while (Check(TokenType.Equal) || Check(TokenType.NotEqual) || 
                   Check(TokenType.Less) || Check(TokenType.Greater) ||
                   Check(TokenType.LessEqual) || Check(TokenType.GreaterEqual)) {
                string op = Consume().Value;
                var right = ParseAdditive();
                left = new BinaryOpNode { Left = left, Op = op, Right = right };
            }
            return left;
        }

        private AstNode ParseAdditive() {
            var left = ParseMultiplicative();
            while (Check(TokenType.Plus) || Check(TokenType.Minus)) {
                string op = Consume().Value;
                var right = ParseMultiplicative();
                left = new BinaryOpNode { Left = left, Op = op, Right = right };
            }
            return left;
        }

        // 【新增】乘除取餘優先級
        private AstNode ParseMultiplicative() {
            var left = ParsePrimary();
            while (Check(TokenType.Star) || Check(TokenType.Slash) || Check(TokenType.Percent)) {
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
