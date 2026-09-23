using System;
using System.Collections.Generic;
using LithosNet.Core;

namespace LithosNet.Compiler {
    public enum TokenType {
        IntLiteral, StringLiteral, Identifier,
        Keyword_Int, Keyword_String, Keyword_Void, Keyword_Return,
        Assign, Semicolon, Comma,
        LeftParen, RightParen, LeftBrace, RightBrace,
        EOF
    }

    public struct Token {
        public TokenType Type;
        public string Value;
        public int Line;
        public Token(TokenType type, string value, int line = 0) {
            Type = type; Value = value; Line = line;
        }
    }

    public class Lexer {
        private readonly string _source;
        private int _pos;
        private int _line = 1;

        public Lexer(string source) { _source = source; _pos = 0; }

        private char Peek => _pos < _source.Length ? _source[_pos] : '\0';
        private char Advance() { char c = _source[_pos++]; if (c == '\n') _line++; return c; }
        private bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        private bool IsAsciiDigit(char c) => c >= '0' && c <= '9';
        private bool IsIdentChar(char c) => IsAsciiLetter(c) || IsAsciiDigit(c) || c == '_';

        public List<Token> Tokenize() {
            var tokens = new List<Token>();
            while (_pos < _source.Length) {
                char c = Peek;

                // 空白
                if (char.IsWhiteSpace(c)) { Advance(); continue; }

                // 單行註解 //
                if (c == '/' && _pos + 1 < _source.Length && _source[_pos + 1] == '/') {
                    while (_pos < _source.Length && Peek != '\n') Advance();
                    continue;
                }

                // 多行註解 /* ... */
                if (c == '/' && _pos + 1 < _source.Length && _source[_pos + 1] == '*') {
                    Advance(); Advance();
                    while (_pos + 1 < _source.Length && !(Peek == '*' && _source[_pos + 1] == '/')) Advance();
                    if (_pos + 1 < _source.Length) { Advance(); Advance(); }
                    continue;
                }

                // 符號
                switch (c) {
                    case '=': Advance(); tokens.Add(new Token(TokenType.Assign, "=", _line)); continue;
                    case ';': Advance(); tokens.Add(new Token(TokenType.Semicolon, ";", _line)); continue;
                    case ',': Advance(); tokens.Add(new Token(TokenType.Comma, ",", _line)); continue;
                    case '(': Advance(); tokens.Add(new Token(TokenType.LeftParen, "(", _line)); continue;
                    case ')': Advance(); tokens.Add(new Token(TokenType.RightParen, ")", _line)); continue;
                    case '{': Advance(); tokens.Add(new Token(TokenType.LeftBrace, "{", _line)); continue;
                    case '}': Advance(); tokens.Add(new Token(TokenType.RightBrace, "}", _line)); continue;
                }

                // 字串 "..."
                if (c == '"') {
                    Advance();
                    int start = _pos;
                    while (_pos < _source.Length && Peek != '"') Advance();
                    tokens.Add(new Token(TokenType.StringLiteral, _source[start.._pos], _line));
                    if (_pos < _source.Length) Advance();
                    continue;
                }

                // 數字
                if (IsAsciiDigit(c)) {
                    int start = _pos;
                    while (_pos < _source.Length && IsAsciiDigit(Peek)) Advance();
                    tokens.Add(new Token(TokenType.IntLiteral, _source[start.._pos], _line));
                    continue;
                }

                // 標識符 / 關鍵字
                if (IsAsciiLetter(c) || c == '_') {
                    int start = _pos;
                    while (_pos < _source.Length && IsIdentChar(Peek)) Advance();
                    string word = _source[start.._pos];
                    var type = word switch {
                        "int" => TokenType.Keyword_Int,
                        "string" => TokenType.Keyword_String,
                        "void" => TokenType.Keyword_Void,
                        "return" => TokenType.Keyword_Return,
                        _ => TokenType.Identifier
                    };
                    tokens.Add(new Token(type, word, _line));
                    continue;
                }

                Advance(); // 忽略未知字元
            }
            tokens.Add(new Token(TokenType.EOF, "", _line));
            return tokens;
        }
    }
}
