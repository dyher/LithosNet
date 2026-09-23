#nullable disable
using System;
using System.Collections.Generic;
using LithosNet.Core;

namespace LithosNet.Compiler {
    public enum TokenType {
        IntLiteral, StringLiteral, Identifier,
        Keyword_Int, Keyword_String, Keyword_Void, Keyword_Mapping, Keyword_Inherit, Keyword_Return, 
        Keyword_If, Keyword_Else, Keyword_While, Keyword_For,
        Assign, Semicolon, Comma, Colon,
        LeftParen, RightParen, LeftBrace, RightBrace, LeftBracket, RightBracket,
        Equal, NotEqual, Less, Greater, LessEqual, GreaterEqual,
        Plus, Minus, Star, Slash, Percent,
        Arrow, 
        // 【新增】語法糖運算符
        And, Or, PlusAssign, MinusAssign, PlusPlus, MinusMinus,
        EOF
    }

    public struct Token {
        public TokenType Type;
        public string Value;
        public int Line;
        public Token(TokenType type, string value, int line = 0) { Type = type; Value = value; Line = line; }
    }

    public class Lexer {
        private readonly string _source;
        private int _pos;
        private int _line = 1;

        public Lexer(string source) { _source = source; _pos = 0; }

        private char Peek => _pos < _source.Length ? _source[_pos] : '\0';
        private char PeekNext => _pos + 1 < _source.Length ? _source[_pos + 1] : '\0';
        private char Advance() { char c = _source[_pos++]; if (c == '\n') _line++; return c; }
        private bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        private bool IsAsciiDigit(char c) => c >= '0' && c <= '9';
        private bool IsIdentChar(char c) => IsAsciiLetter(c) || IsAsciiDigit(c) || c == '_';

        public List<Token> Tokenize() {
            var tokens = new List<Token>();
            while (_pos < _source.Length) {
                char c = Peek;
                if (char.IsWhiteSpace(c)) { Advance(); continue; }
                if (c == '/' && PeekNext == '/') { while (_pos < _source.Length && Peek != '\n') Advance(); continue; }
                if (c == '/' && PeekNext == '*') { Advance(); Advance(); while (_pos + 1 < _source.Length && !(Peek == '*' && PeekNext == '/')) Advance(); if (_pos + 1 < _source.Length) { Advance(); Advance(); } continue; }

                // 【新增】雙字元與三字元運算符
                if (c == '&' && PeekNext == '&') { Advance(); Advance(); tokens.Add(new Token(TokenType.And, "&&", _line)); continue; }
                if (c == '|' && PeekNext == '|') { Advance(); Advance(); tokens.Add(new Token(TokenType.Or, "||", _line)); continue; }
                if (c == '+' && PeekNext == '+') { Advance(); Advance(); tokens.Add(new Token(TokenType.PlusPlus, "++", _line)); continue; }
                if (c == '-' && PeekNext == '-') { Advance(); Advance(); tokens.Add(new Token(TokenType.MinusMinus, "--", _line)); continue; }
                if (c == '+' && PeekNext == '=') { Advance(); Advance(); tokens.Add(new Token(TokenType.PlusAssign, "+=", _line)); continue; }
                if (c == '-' && PeekNext == '=') { Advance(); Advance(); tokens.Add(new Token(TokenType.MinusAssign, "-=", _line)); continue; }
                
                if (c == '=' && PeekNext == '=') { Advance(); Advance(); tokens.Add(new Token(TokenType.Equal, "==", _line)); continue; }
                if (c == '!' && PeekNext == '=') { Advance(); Advance(); tokens.Add(new Token(TokenType.NotEqual, "!=", _line)); continue; }
                if (c == '<' && PeekNext == '=') { Advance(); Advance(); tokens.Add(new Token(TokenType.LessEqual, "<=", _line)); continue; }
                if (c == '>' && PeekNext == '=') { Advance(); Advance(); tokens.Add(new Token(TokenType.GreaterEqual, ">=", _line)); continue; }
                if (c == '-' && PeekNext == '>') { Advance(); Advance(); tokens.Add(new Token(TokenType.Arrow, "->", _line)); continue; }

                switch (c) {
                    case '=': Advance(); tokens.Add(new Token(TokenType.Assign, "=", _line)); continue;
                    case ';': Advance(); tokens.Add(new Token(TokenType.Semicolon, ";", _line)); continue;
                    case ',': Advance(); tokens.Add(new Token(TokenType.Comma, ",", _line)); continue;
                    case ':': Advance(); tokens.Add(new Token(TokenType.Colon, ":", _line)); continue;
                    case '(': Advance(); tokens.Add(new Token(TokenType.LeftParen, "(", _line)); continue;
                    case ')': Advance(); tokens.Add(new Token(TokenType.RightParen, ")", _line)); continue;
                    case '{': Advance(); tokens.Add(new Token(TokenType.LeftBrace, "{", _line)); continue;
                    case '}': Advance(); tokens.Add(new Token(TokenType.RightBrace, "}", _line)); continue;
                    case '[': Advance(); tokens.Add(new Token(TokenType.LeftBracket, "[", _line)); continue;
                    case ']': Advance(); tokens.Add(new Token(TokenType.RightBracket, "]", _line)); continue;
                    case '<': Advance(); tokens.Add(new Token(TokenType.Less, "<", _line)); continue;
                    case '>': Advance(); tokens.Add(new Token(TokenType.Greater, ">", _line)); continue;
                    case '+': Advance(); tokens.Add(new Token(TokenType.Plus, "+", _line)); continue;
                    case '-': Advance(); tokens.Add(new Token(TokenType.Minus, "-", _line)); continue;
                    case '*': Advance(); tokens.Add(new Token(TokenType.Star, "*", _line)); continue;
                    case '%': Advance(); tokens.Add(new Token(TokenType.Percent, "%", _line)); continue;
                }

                if (c == '/') { Advance(); tokens.Add(new Token(TokenType.Slash, "/", _line)); continue; }

                if (c == '"') { Advance(); int start = _pos; while (_pos < _source.Length && Peek != '"') Advance(); tokens.Add(new Token(TokenType.StringLiteral, _source[start.._pos], _line)); if (_pos < _source.Length) Advance(); continue; }
                if (IsAsciiDigit(c)) { int start = _pos; while (_pos < _source.Length && IsAsciiDigit(Peek)) Advance(); tokens.Add(new Token(TokenType.IntLiteral, _source[start.._pos], _line)); continue; }
                
                if (IsAsciiLetter(c) || c == '_') {
                    int start = _pos;
                    while (_pos < _source.Length && IsIdentChar(Peek)) Advance();
                    string word = _source[start.._pos];
                    var type = word switch {
                        "int" => TokenType.Keyword_Int, "string" => TokenType.Keyword_String,
                        "void" => TokenType.Keyword_Void, "mapping" => TokenType.Keyword_Mapping,
                        "inherit" => TokenType.Keyword_Inherit, "return" => TokenType.Keyword_Return,
                        "if" => TokenType.Keyword_If, "else" => TokenType.Keyword_Else,
                        "while" => TokenType.Keyword_While, "for" => TokenType.Keyword_For,
                        _ => TokenType.Identifier
                    };
                    tokens.Add(new Token(type, word, _line));
                    continue;
                }
                Advance();
            }
            tokens.Add(new Token(TokenType.EOF, "", _line));
            return tokens;
        }
    }
}
