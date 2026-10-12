# LithosNet V4 - neolith 1:1 復刻

## 當前進度 P43 119 green
- `test_simple 203` 純淨 F_LONG & 255
- `test_inherit 42` / `test_inherit_true 52` 真文件 inherit base.c 2 funcs
- `test_switch 119` switch/case/default/break BRANCH_IF_TRUE chain
- `test_object 42` / `test_object_arg 42` / filter 2 / map 12 / sort 123 / closure 30

## 目錄 (已清理)
```
src/compiler/  # Lex.cs Grammar.cs ParseTree.cs CodeGenerator.cs 真復刻 (P43)
src/vm/        # SValue.cs Interpreter.cs Program.cs
src/tests/     # test_simple.c 等純淨測試
archive/old_qwen/  # P10-P35 qwen 臆測版考古 (不編譯)
docs/
README.md .gitignore LithosNet_V4.csproj
```

## 編譯
dotnet build -c Release
dotnet run -- --run./test_simple.c  # 203
dotnet run -- --run./test_switch.c  # 119
dotnet run -- --run./test_inherit_true.c  # 52

## 路線
P44 class/struct ::調父 -> P45 300 efun -> P46 object table/heart_beat/call_out = mmorpg driver
-> LPC 復刻 rathena -> galaxy2d + ggelpc (比 ggelua 只提供部分網遊框架功能完整)
