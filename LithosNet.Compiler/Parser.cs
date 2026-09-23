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

        // 【極簡化】ParseStatement 不再處理賦值，全部交給 ParseExpression！
        private AstNode ParseStatement() {
            if (Check(TokenType.Keyword_Return)) { Consume(); var v = ParseExpression(); Expect(TokenType.Semicolon); return new ReturnNode { Value = v }; }
            if (Check(TokenType.Keyword_If)) return ParseIfStatement();
            if (Check(TokenType.Keyword_While)) return ParseWhileStatement();
            if (Check(TokenType.Keyword_For)) return ParseForStatement();
            if (IsVariableDeclaration()) return ParseVariableDeclaration();
            
            var expr = ParseExpression();
            Expect(TokenType.Semicolon);
            return expr;
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

        private AstNode ParseForStatement() {
            Consume(); Expect(TokenType.LeftParen);
            AstNode init = null;
            if (!Check(TokenType.Semicolon)) {
                if (IsVariableDeclaration()) init = ParseVariableDeclaration();
                else { init = ParseExpression(); Expect(TokenType.Semicolon); }
            } else { Consume(); }
            
            AstNode cond = null;
            if (!Check(TokenType.Semicolon)) cond = ParseExpression();
            Expect(TokenType.Semicolon);
            
            AstNode step = null;
            if (!Check(TokenType.RightParen)) step = ParseExpression(); // 【關鍵】這裡現在能完美解析 i = i + 1 了！
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

        // 【教科書級別的優先級鏈】
        // 1. 表達式入口
        private AstNode ParseExpression() { return ParseAssignment(); }

        // 2. 賦值優先級最低，且為右結合 (支援 a = b = 5)
        private AstNode ParseAssignment() {
            var expr = ParseComparison();
            if (Check(TokenType.Assign)) {
                Consume();
                var val = ParseAssignment(); // 右結合遞迴
                if (expr is VariableRefNode vref) 
                    return new AssignmentNode { VariableName = vref.Name, Value = val };
                if (expr is IndexAccessNode idx) 
                    return new IndexAssignmentNode { Array = idx.Array, Index = idx.Index, Value = val };
                throw new Exception($"[Parser] Line {Current.Line}: Invalid assignment target");
            }
            return expr;
        }

        // 3. 比較運算符
        private AstNode ParseComparison() {
            var left = ParseAdditive();
            while (Check(TokenType.Equal) || Check(TokenType.NotEqual) || 
                   Check(TokenType.Less) || Check(TokenType.Greater) ||
                   Check(TokenType.LessEqual) || Check(TokenType.GreaterEqual)) {
                string op = Consume().Value;
                left = new BinaryOpNode { Left = left, Op = op, Right = ParseAdditive() };
            }
            return left;
        }

        // 4. 加減法
        private AstNode ParseAdditive() {
            var left = ParseMultiplicative();
            while (Check(TokenType.Plus) || Check(TokenType.Minus)) {
                string op = Consume().Value;
                left = new BinaryOpNode { Left = left, Op = op, Right = ParseMultiplicative() };
            }
            return left;
        }

        // 5. 乘除與取餘
        private AstNode ParseMultiplicative() {
            var left = ParsePrimary();
            while (Check(TokenType.Star) || Check(TokenType.Slash) || Check(TokenType.Percent)) {
                string op = Consume().Value;
                left = new BinaryOpNode { Left = left, Op = op, Right = ParsePrimary() };
            }
            return left;
        }

        // 6. 基礎元素 (Primary)
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
