import re

# 1. 檢查並修復 AstBuilder.cs
with open('LithosNet.Compiler/AstBuilder.cs', 'r', encoding='utf-8') as f:
    builder = f.read()

# 如果沒有 VisitMappingLiteral，我們需要確保它被正確路由
# 這裡我們先印出狀態，並嘗試修復最常見的誤判
if 'VisitMappingLiteral' not in builder and 'MappingLiteralNode' in builder:
    print("⚠ AstBuilder 中可能缺少 VisitMappingLiteral 的實作！")

# 2. 檢查 LPC.g4 中的 mapping 規則
with open('./LithosNet.Compiler/Grammar/LPC.g4', 'r', encoding='utf-8') as f:
    g4 = f.read()

# 尋找類似 mappingLiteral : '(' '[' (expr ':' expr (',' expr ':' expr)*)? ']' ')' ;
if 'mappingLiteral' not in g4 and '([' in g4:
    print("⚠ LPC.g4 中可能將 ([' 統一歸類為 array 了！")
    
print("🔍 請查看上方印出的真實 Grammar 與 AstBuilder 代碼！")
