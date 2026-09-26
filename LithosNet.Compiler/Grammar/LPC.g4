
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
SWITCH : 'switch' ; CASE : 'case' ; DEFAULT : 'default' ; BREAK : 'break' ;

ID          : [a-zA-Z_][a-zA-Z0-9_]* ;
INT_LITERAL : '-'? [0-9]+ ;
STRING_LITERAL : '"' (~["\\\r\n] | '\\' .)* '"' ;

PLUS        : '+' ; MINUS       : '-' ; STAR        : '*' ; SLASH       : '/' ;
ASSIGN      : '=' ; EQ          : '==' ; NEQ         : '!=' ;
GT          : '>' ; LT          : '<' ; GTE         : '>=' ; LTE         : '<=' ;
AND         : '&&' ; OR         : '||' ; NOT         : '!' ;

ARRAY_OPEN  : '({' ;
ARRAY_CLOSE : '})' ;
MAP_OPEN    : '([' ;
MAP_CLOSE   : '])' ;
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
    | switchStmt
    | breakStmt
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
    : arrayLiteral
    | mappingLiteral
    | ID
    | INT_LITERAL
    | STRING_LITERAL
    | LPAREN expr RPAREN
    
    ;

arrayLiteral : ARRAY_OPEN (expr (COMMA expr)*)? ARRAY_CLOSE ;

mappingLiteral : MAP_OPEN (expr COLON expr (COMMA expr COLON expr)*)? MAP_CLOSE ;
