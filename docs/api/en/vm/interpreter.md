# Interpreter VM - English

## Overview
InterpreterP9 / InterpreterV4 is 1:1 port of 【entity-Neolith¦canonical_name=Neolith】 LPC VM.

## Architecture
- SValueS: T_NUMBER, T_STRING, T_ARRAY, T_MAPPING, T_FUNCTION, T_OBJECT
- Program: Code, Strings, FuncTable, FuncAddr
- InterpreterP9.Inner: SP/FP stack machine
- InterpreterV4: entry fixed to FuncAddr[0], was bug using last closure

## Key Opcodes
- F_CONST0=14, F_CONST1=15, F_BYTE=10 - fixed bug InsByte(1) decoded as F_EFUN0(2)
- F_BRANCH=20, F_BBRANCH=23, F_BRANCH_WHEN_ZERO=18
- F_ASSIGN=90, F_VOID_ASSIGN=92, F_LOCAL=60, F_LOCAL_LVALUE=61
- F_ADD=94, F_EQ=47, F_INDEX=5, F_INDEX_LVALUE=80
- F_CALL, F_CALL_OTHER, F_EVALUATE closure

## Stack Rules
LVALUE must be LVALUE x; VALUE; VOID_ASSIGN, not LVALUE,POP

## Tests
test_all.c ALL PASS
