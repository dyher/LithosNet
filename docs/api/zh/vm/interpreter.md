# Interpreter 虛擬機 - 中文

## 概述
InterpreterP9 / InterpreterV4 1:1 復刻 【entity-Neolith¦canonical_name=Neolith】 LPC 虛擬機。

## 架構
- SValueS: T_NUMBER, T_STRING, T_ARRAY, T_MAPPING, T_FUNCTION(閉包), T_OBJECT
- Program: Code, Strings, FuncTable, FuncAddr
- InterpreterP9.Inner: SP/FP 堆疊機
- InterpreterV4: 入口修為 FuncAddr[0]，原 bug 用 FuncAddr[Count-1]

## 關鍵 Opcodes
- F_CONST0=14, F_CONST1=15, F_BYTE=10 - 常數編碼，修過 InsByte(1) 誤用為 F_EFUN0(2)
- F_BRANCH=20, F_BBRANCH=23, F_BRANCH_WHEN_ZERO=18
- F_ASSIGN=90, F_VOID_ASSIGN=92, F_LOCAL=60, F_LOCAL_LVALUE=61
- F_ADD=94, F_EQ=47, F_INDEX=5, F_INDEX_LVALUE=80
- F_CALL, F_CALL_OTHER(->), F_EVALUATE 閉包

## 堆疊規則
LVALUE 必須 LVALUE x; VALUE; VOID_ASSIGN，不能 LVALUE,POP 或 LOCAL x, CONST1, ADD, LVALUE, POP

## 測試
test_all.c -> filter/map/sort/for++/--/&&||!/concat ALL PASS
