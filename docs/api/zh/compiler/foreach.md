# foreach / for / ++ -- / && || ! - 中文

## ++/-- 
Lex 回傳 ++ 為 Punct，舊 ParseUnary 檢查 TokenType.Operator 導致 for(i++) 失敗。
修為 MatchText("++") / MatchText("--") 支援前綴後綴。

## && || !
- !localVar 舊只處理 local，現通用 operand, F_CONST0, F_EQ
- GenerateAnd/Or true/false InsByte(1)/InsByte(2) -> F_CONST0/F_CONST1
- mapping 佔位 InsByte(1) F_MAP_ADD -> F_CONST0

## foreach 重寫
舊: container KEYS, LVALUE keys, POP ... LOCAL idx, CONST1, ADD, LVALUE idx, POP
新: LVALUE keys; container KEYS; VOID_ASSIGN
     LVALUE k; LOCAL keys; LOCAL idx; INDEX; VOID_ASSIGN
     LVALUE v; container; LOCAL k; INDEX; VOID_ASSIGN
     LVALUE idx; LOCAL idx; CONST1; ADD; VOID_ASSIGN

## foreach(string k, int v in m)
支援型別前綴，新增 SkipType() 跳過 string/int/object

測試: for(i=0;i<5;i++), x++, x--, &&, ||, !0==1, foreach(k,v in m) 全綠
