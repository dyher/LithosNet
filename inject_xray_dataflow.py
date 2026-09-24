import re

# 1. 修改 BuiltInEfuns.cs 的 SendToUser
with open('LithosNet.VM/BuiltInEfuns.cs', 'r', encoding='utf-8') as f:
    efun_code = f.read()

new_send_to_user = """[Efun("send_to_user")]
        public static LpcValue SendToUser(LpcValue[] args) {
            Console.WriteLine($"🔍 [X-Ray] send_to_user 觸發！參數: '{args[0].AsString()}'");
            string obj = SessionManager.CurrentPlayer.Value ?? "";
            Console.WriteLine($"🔍 [X-Ray] CurrentPlayer: '{obj}'");
            
            if (string.IsNullOrEmpty(obj)) {
                var all = SessionManager.GetAllSessions();
                if (all.Count > 0) obj = all[0];
            }
            Console.WriteLine($"🔍 [X-Ray] 最終目標: '{obj}'");
            
            if (!string.IsNullOrEmpty(obj)) {
                SessionManager.SendAsync(obj, args[0].AsString()).GetAwaiter().GetResult();
            } else {
                Console.WriteLine("⚠ [X-Ray] 找不到任何目標，啟動暴力廣播！");
                foreach(var w in SessionManager.GetAllWriters()) {
                    SessionManager.SendAsyncWriter(w, args[0].AsString()).GetAwaiter().GetResult();
                }
            }
            return LpcValue.Create(1);
        }"""

efun_code = re.sub(r'\[Efun\("send_to_user"\)\][^{]*\{[^}]*\}', new_send_to_user, efun_code, flags=re.DOTALL)

with open('LithosNet.VM/BuiltInEfuns.cs', 'w', encoding='utf-8') as f:
    f.write(efun_code)
print("✅ BuiltInEfuns.cs 已注入 X 光追蹤與暴力廣播 Fallback！")

# 2. 修改 SessionManager.cs 加入 SendAsyncWriter 與 X 光日誌
with open('LithosNet.VM/SessionManager.cs', 'r', encoding='utf-8') as f:
    sm_code = f.read()

new_send_async = """public static async Task SendAsync(string objName, string message) {
            Console.WriteLine($"🔍 [X-Ray] SendAsync 目標: '{objName}', 訊息: '{message}'");
            if (_sessions.TryGetValue(objName, out var writer)) {
                await SendAsyncWriter(writer, message);
            } else {
                Console.WriteLine($"⚠ [X-Ray] _sessions 找不到 '{objName}'！");
            }
        }

        public static async Task SendAsyncWriter(PipeWriter writer, string message) {
            Console.WriteLine($"🔍 [X-Ray] SendAsyncWriter 執行！訊息: '{message}'");
            byte[] bytes = Encoding.UTF8.GetBytes(message + "\\n");
            await writer.WriteAsync(bytes);
            await writer.FlushAsync();
            Console.WriteLine("✅ [X-Ray] FlushAsync 完成！資料已推向 Socket！");
        }"""

sm_code = re.sub(r'public static async Task SendAsync\(string objName, string message\) \{[^}]+\}', new_send_async, sm_code, flags=re.DOTALL)

with open('LithosNet.VM/SessionManager.cs', 'w', encoding='utf-8') as f:
    f.write(sm_code)
print("✅ SessionManager.cs 已注入 SendAsyncWriter 與 X 光日誌！")
