# LithosNet V4 - neolith 1:1 復刻

## 當前進度 P43 119 green
- `test_simple 203` 純淨 F_LONG & 255
- `test_inherit 42` / `test_inherit_true 52` 真文件 inherit
- `test_switch 119` switch/case/default/break BRANCH_IF_TRUE chain
- `test_object 42` / `test_object_arg 42` / filter 2 / map 12 / sort 123 / closure 30

## 目錄
```
src/compiler/  # Lex.cs Grammar.cs ParseTree.cs CodeGenerator.cs 真復刻
src/vm/        # SValue.cs Interpreter.cs Program.cs
src/tests/     # test_simple.c 等純淨測試
archive/old_qwen/  # P10-P35 qwen 臆測版考古
archive/old_tests/ # 舊 .bin 和 base.c
```

## 編譯
dotnet build -c Release
dotnet run -- --run./test_simple.c  # 203
dotnet run -- --run./test_switch.c  # 119

## 路線
P44 class/struct ::調父 -> P45 300 efun -> P46 object table/heart_beat/call_out = mmorpg driver
-> LPC 復刻 rathena -> galaxy2d + ggelpc
