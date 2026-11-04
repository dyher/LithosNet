# foreach / for / ++ -- / && || ! - English

## ++/--
Lex returns ++ as Punct, old ParseUnary checked TokenType.Operator causing for(i++) fail.
Fixed to MatchText("++") / MatchText("--") for prefix/postfix.

## && || !
- !localVar old only local, now generic operand, F_CONST0, F_EQ
- GenerateAnd/Or true/false InsByte(1)/2 -> F_CONST0/F_CONST1
- mapping placeholder InsByte(1) F_MAP_ADD -> F_CONST0

## foreach Rewrite
Old: container KEYS, LVALUE keys, POP ... LOCAL idx, CONST1, ADD, LVALUE idx, POP
New: LVALUE keys; container KEYS; VOID_ASSIGN
     LVALUE k; LOCAL keys; LOCAL idx; INDEX; VOID_ASSIGN
     LVALUE v; container; LOCAL k; INDEX; VOID_ASSIGN
     LVALUE idx; LOCAL idx; CONST1; ADD; VOID_ASSIGN

## foreach(string k, int v in m)
Supports type prefix, added SkipType() to skip type keywords.

Tests: for(i=0;i<5;i++), x++, x--, &&, ||, !0, foreach(k,v in m) green
