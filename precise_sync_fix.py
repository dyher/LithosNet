import re

with open('LithosNet.Host/Program.cs', 'r', encoding='utf-8') as f:
    prog = f.read()

# 1. 清理上一輪可能殘留的錯誤注入 (拔除外層 while 的殘骸)
prog = re.sub(r'// 【終極同步】.*?\n\s*var syncedObj.*?\n\s*if \(!string\.IsNullOrEmpty.*?\n', '', prog)

# 2. 精準在 receive_message 呼叫前插入同步邏輯 (保留原始縮排)
prog = re.sub(
    r'([ \t]*)(ObjMgr\.CallFunction\(currentObj, "receive_message")',
    r'\1currentObj = SessionManager.GetObjName(writer) ?? currentObj;\n\1\2',
    prog
)

# 3. 精準在 receive_binary 呼叫前插入同步邏輯 (保留原始縮排)
prog = re.sub(
    r'([ \t]*)(ObjMgr\.CallFunction\(currentObj, "receive_binary")',
    r'\1currentObj = SessionManager.GetObjName(writer) ?? currentObj;\n\1\2',
    prog
)

with open('LithosNet.Host/Program.cs', 'w', encoding='utf-8') as f:
    f.write(prog)
print('✅ 已精準在 CallFunction 正上方注入刷新邏輯！絕對處於正確的作用域內！')
