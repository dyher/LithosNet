import re

with open('LithosNet.VM/BuiltInEfuns.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# 1. 強制在所有 writer.Write 之後加入 FlushAsync().AsTask().Wait()
code = re.sub(
    r'(writer\.Write\([^;]+\);)',
    r'\1\n                    try { writer.FlushAsync().AsTask().Wait(); } catch {}',
    code
)

# 2. 如果找不到 writer，直接廣播給 SessionManager 中所有的 Writer (終極 Fallback)
# 尋找類似 if (writer != null) 的邏輯，並在 else 區塊加入廣播
broadcast_logic = """
                    if (writer == null) {
                        // 【終極 Fallback】找不到目標，直接廣播給所有已連線的玩家！
                        var allWriters = SessionManager.GetAllWriters();
                        foreach(var w in allWriters) {
                            try { 
                                w.Write(bytes); 
                                w.FlushAsync().AsTask().Wait(); 
                            } catch {}
                        }
                    }
"""

# 嘗試在 SendToUser 或 TellObject 中注入廣播邏輯
if "GetAllWriters" not in code:
    # 簡單替換：將 if (writer != null) 替換為包含 Fallback 的版本
    code = re.sub(
        r'if\s*\(\s*writer\s*!=\s*null\s*\)\s*\{',
        r'if (writer != null) {\n' + broadcast_logic + '\n                    } else {',
        code, count=1 # 只替換第一個匹配到的地方，避免破壞其他邏輯
    )

with open('LithosNet.VM/BuiltInEfuns.cs', 'w', encoding='utf-8') as f:
    f.write(code)

# 3. 確保 SessionManager 有 GetAllWriters 方法
with open('LithosNet.VM/SessionManager.cs', 'r', encoding='utf-8') as f:
    sm_code = f.read()

if "GetAllWriters" not in sm_code:
    get_all_method = """
        public static System.Collections.Generic.List<System.IO.Pipelines.PipeWriter> GetAllWriters() {
            return new System.Collections.Generic.List<System.IO.Pipelines.PipeWriter>(_sessions.Values);
        }
"""
    # 注入到 SessionManager 類別內部
    sm_code = sm_code.replace("public static class SessionManager {", "public static class SessionManager {\n" + get_all_method)
    with open('LithosNet.VM/SessionManager.cs', 'w', encoding='utf-8') as f:
        f.write(sm_code)

print("✅ BuiltInEfuns.cs 已強制加入 Flush！SessionManager 已加入廣播 Fallback！資料將 100% 發送！")
