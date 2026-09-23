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

        private void Expect(TokenType type) {
            if (Current.Type != type)
                throw new Exception($"[Parser] Line {Current.Line}: Expected {type}, got {Current.Type} ('{Current.Value}')");
            Consume();
        }

        public List<AstNode> Parse() {
            var nodes = new List<AstNode>();
            while (!Check(TokenType.EOF)) {
                nodes.Add(ParseTopLevel());
            }
            return nodes;
        }

        private AstNode ParseTopLevel() {
            if (!IsTypeKeyword())
                throw new Exception($"[Parser] Line {Current.Line}: Unexpected token {Current.Type} ('{Current.Value}')");

            // 偷看下一個 token：如果是 IDENTIFIER + '('，就是函數宣告
            if (_pos + 2 < _tokens.Count &&
                _tokens[_pos + 1].Type == TokenType.Identifier &&
                _tokens[_pos + 2].Type == TokenType.LeftParen) {
                return ParseFunctionDeclaration();
            }

            return ParseVariableDeclaration();
        }

        // int x = 10;
        private AstNode ParseVariableDeclaration() {
            string typeName = Consume().Value;
            string varName = Consume().Value;
            AstNode init = null;
            if (Check(TokenType.Assign)) {
                Consume();
                init = ParseExpression();
            }
            Expect(TokenType.Semicolon);
            return new VariableDeclarationNode { TypeName = typeName, VariableName = varName, Initializer = init };
        }

        // void logon(string user) { ... }
        private AstNode ParseFunctionDeclaration() {
            string retType = Consume().Value;
            string funcName = Consume().Value;

            Expect(TokenType.LeftParen);
            var parameters = new List<ParameterNode>();
            while (!Check(TokenType.RightParen)) {
                string pType = Consume().Value;
                string pName = Consume().Value;
                parameters.Add(new ParameterNode { TypeName = pType, Name = pName });
                if (Check(TokenType.Comma)) Consume();
            }
            Expect(TokenType.RightParen);

            Expect(TokenType.LeftBrace);
            var body = new List<AstNode>();
            while (!Check(TokenType.RightBrace) && !Check(TokenType.EOF)) {
                body.Add(ParseStatement());
            }
            Expect(TokenType.RightBrace);

            return new FunctionDeclarationNode {
                ReturnType = retType, Name = funcName,
                Parameters = parameters, Body = body
            };
        }

        private AstNode ParseStatement() {
            if (Check(TokenType.Keyword_Return)) {
                Consume();
                AstNode val = ParseExpression();
                Expect(TokenType.Semicolon);
                return new ReturnNode { Value = val };
            }
            if (IsTypeKeyword() && _pos + 2 < _tokens.Count && _tokens[_pos + 1].Type == TokenType.Identifier
                && _tokens[_pos + 2].Type != TokenType.LeftParen) {
                return ParseVariableDeclaration();
            }
            // 函數呼叫語句: do_something();
            var expr = ParseExpression();
            Expect(TokenType.Semicolon);
            return expr;
        }

        private AstNode ParseExpression() {
            if (Check(TokenType.IntLiteral)) {
                return new LiteralNode { Value = LpcValue.Create(int.Parse(Consume().Value)) };
            }
            if (Check(TokenType.StringLiteral)) {
                return new LiteralNode { Value = LpcValue.Create(Consume().Value) };
            }
            if (Check(TokenType.Identifier)) {
                string name = Consume().Value;
                // 函數呼叫: name(args)
                if (Check(TokenType.LeftParen)) {
                    Consume();
                    var args = new List<AstNode>();
                    while (!Check(TokenType.RightParen)) {
                        args.Add(ParseExpression());
                        if (Check(TokenType.Comma)) Consume();
                    }
                    Expect(TokenType.RightParen);
                    return new FunctionCallNode { Name = name, Arguments = args };
                }
                return new VariableRefNode { Name = name };
            }
            throw new Exception($"[Parser] Line {Current.Line}: Cannot parse expression for {Current.Type} ('{Current.Value}')");
        }
    }
}
