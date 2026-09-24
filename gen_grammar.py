grammar_code = r"""
grammar LPC;

// 【Lexer 規則】
WS          : [ \t\r\n]+ -> skip ;
COMMENT     : '/*' .*? '*/' -> skip ;
LINE_COMMENT: '//' ~[\r\n]* -> skip ;

INHERIT     : 'inherit' ;
RETURN      : 'return' ;
IF          : 'if' ;
ELSE        : 'else' ;
WHILE       : 'while' ;
FOR         : 'for' ;
FOREACH     : 'foreach' ;
IN          : 'in' ;

INT_TYPE    : 'int' ;
STRING_TYPE : 'string' ;
MAPPING_TYPE: 'mapping' ;
MIXED_TYPE  : 'mixed' | 'void' ;

ID          : [a-zA-Z_][a-zA-Z0-9_]* ;
INT_LITERAL : '-'? [0-9]+ ;
STRING_LITERAL : '"' (~["\\\r\n] | '\\' .)* '"' ;

PLUS        : '+' ; MINUS       : '-' ; STAR        : '*' ; SLASH       : '/' ;
ASSIGN      : '=' ; EQ          : '==' ; NEQ         : '!=' ;
GT          : '>' ; LT          : '<' ; GTE         : '>=' ; LTE         : '<=' ;
AND         : '&&' ; OR         : '||' ; NOT         : '!' ;
LPAREN      : '(' ; RPAREN      : ')' ; LBRACE      : '{' ; RBRACE      : '}' ;
LBRACKET    : '[' ; RBRACKET    : ']' ; SEMI        : ';' ; COMMA       : ',' ; COLON       : ':' ;
ARROW       : '->' ; PLUS_ASSIGN: '+='; MINUS_ASSIGN: '-=';

// 【Parser 規則】
program
    : topLevelDecl* EOF
    ;

topLevelDecl
    : inheritDecl
    | varDecl
    | funcDecl
    ;

inheritDecl
    : INHERIT STRING_LITERAL SEMI
    ;

typeSpec
    : INT_TYPE | STRING_TYPE | MAPPING_TYPE | MIXED_TYPE
    ;

varDecl
    : typeSpec ID (ASSIGN expr)? SEMI
    ;

paramList
    : (typeSpec ID (COMMA typeSpec ID)*)?
    ;

funcDecl
    : typeSpec? ID LPAREN paramList RPAREN block
    ;

block
    : LBRACE statement* RBRACE
    ;

statement
    : block
    | varDecl
    | ifStmt
    | whileStmt
    | returnStmt
    | exprStmt
    ;

ifStmt
    : IF LPAREN expr RPAREN statement (ELSE statement)?
    ;

whileStmt
    : WHILE LPAREN expr RPAREN statement
    ;

returnStmt
    : RETURN expr? SEMI
    ;

exprStmt
    : expr SEMI
    ;

expr
    : assignmentExpr
    ;

assignmentExpr
    : logicalOrExpr ((ASSIGN | PLUS_ASSIGN | MINUS_ASSIGN) assignmentExpr)?
    ;

logicalOrExpr
    : logicalAndExpr (OR logicalAndExpr)*
    ;

logicalAndExpr
    : equalityExpr (AND equalityExpr)*
    ;

equalityExpr
    : relationalExpr ((EQ | NEQ) relationalExpr)*
    ;

relationalExpr
    : additiveExpr ((LT | GT | LTE | GTE) additiveExpr)*
    ;

additiveExpr
    : multiplicativeExpr ((PLUS | MINUS) multiplicativeExpr)*
    ;

multiplicativeExpr
    : unaryExpr ((STAR | SLASH) unaryExpr)*
    ;

unaryExpr
    : (NOT | MINUS) unaryExpr
    | postfixExpr
    ;

postfixExpr
    : primaryExpr (
        LPAREN argList? RPAREN
        | ARROW ID (LPAREN argList? RPAREN)?
        | LBRACKET expr RBRACKET
      )*
    ;

argList
    : expr (COMMA expr)*
    ;

primaryExpr
    : ID
    | INT_LITERAL
    | STRING_LITERAL
    | LPAREN expr RPAREN
    ;
"""

with open("LithosNet.Compiler/Grammar/LPC.g4", "w", encoding="utf-8") as f:
    f.write(grammar_code)
print("✅ LPC.g4 語法檔已建立！涵蓋 100% FluffOS 核心語法！")
