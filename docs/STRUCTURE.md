# 清理說明
- 根目錄只留: src/, docs/, README.md, .gitignore, LithosNet.sln
- 所有 CodeGenerator_Pxx / Grammar_Pxx / Lex_Pxx 已移到 archive/old_qwen，不影響編譯
- 綠版: src/compiler/CodeGenerator.cs = P43 switch fix 119, Grammar.cs = P43 switch
- 未來新 P44 直接覆蓋 src/compiler/ 下對應檔案
