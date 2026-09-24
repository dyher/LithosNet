with open('LithosNet.Host/Program.cs', 'r', encoding='utf-8') as f:
    prog = f.read()

# 鎖定最穩固的錨點：while (true) {
old_loop = 'while (true) {'
new_loop = '''while (true) {
                // 【終極同步】每次收到封包前，強制刷新 currentObj 為 Session 綁定的最新物件 (處理 exec 轉移)
                var syncedObj = SessionManager.GetObjName(writer);
                if (!string.IsNullOrEmpty(syncedObj)) currentObj = syncedObj;'''

if old_loop in prog and 'syncedObj' not in prog:
    prog = prog.replace(old_loop, new_loop, 1)
    with open('LithosNet.Host/Program.cs', 'w', encoding='utf-8') as f:
        f.write(prog)
    print("✅ 已成功在 while(true) 迴圈頂端注入 currentObj 刷新邏輯！")
else:
    print("⚠️ 找不到 while (true) { 或已存在修復邏輯。")
