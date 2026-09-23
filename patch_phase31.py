import re

with open("LithosNet.VM/Interpreter.cs", "r", encoding="utf-8") as f:
    content = f.read()

# 【核心注入】sprintf, tell_object, shout, native_call (FFI)
new_efuns = """
                    // 【Mudlib 靈魂】sprintf 格式化輸出
                    if (c.Name == "sprintf" && cArgs.Count >= 1) {
                        string fmt = cArgs[0].AsString();
                        int argIdx = 1;
                        var sb = new System.Text.StringBuilder();
                        for(int i=0; i<fmt.Length; i++) {
                            if (fmt[i] == '%' && i+1 < fmt.Length && argIdx < cArgs.Count) {
                                char next = fmt[i+1];
                                if (next == 's' || next == 'd' || next == 'i' || next == 'O' || next == 'o') {
                                    sb.Append(cArgs[argIdx].ToString());
                                    argIdx++; i++; continue;
                                }
                            }
                            sb.Append(fmt[i]);
                        }
                        return LpcValue.Create(sb.ToString());
                    }
                    
                    // 【Mudlib 通訊】tell_object 與 shout
                    if (c.Name == "tell_object" && cArgs.Count >= 2) {
                        SessionManager.SendAsync(cArgs[0].AsString(), cArgs[1].AsString());
                        return LpcValue.Create(1);
                    }
                    if (c.Name == "shout" && cArgs.Count >= 1) {
                        foreach(var s in SessionManager.GetAllSessions()) SessionManager.SendAsync(s, cArgs[0].AsString());
                        return LpcValue.Create(1);
                    }

                    // 【God Mode FFI】LuaJIT 風格的 native_call，直接呼叫底層 C .so！
                    if (c.Name == "native_call" && cArgs.Count >= 2) {
                        string lib = cArgs[0].AsString();
                        string func = cArgs[1].AsString();
                        try {
                            IntPtr handle = System.Runtime.InteropServices.NativeLibrary.Load(lib);
                            IntPtr ptr = System.Runtime.InteropServices.NativeLibrary.GetExport(handle, func);
                            
                            // 範例 1: 呼叫無參數，返回 int 的函數 (如 getpid)
                            if (func == "getpid" || func == "time") {
                                var del = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<Func<int>>(ptr);
                                return LpcValue.Create(del());
                            }
                            // 範例 2: 呼叫傳入 string，返回 string (指標) 的函數 (如 getenv)
                            if (func == "getenv" && cArgs.Count >= 3) {
                                var del = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<Func<string, IntPtr>>(ptr);
                                IntPtr res = del(cArgs[2].AsString());
                                return LpcValue.Create(System.Runtime.InteropServices.Marshal.PtrToStringAnsi(res) ?? "");
                            }
                            return LpcValue.Create(0);
                        } catch (Exception ex) {
                            Console.WriteLine($"⚠️ FFI 錯誤: {ex.Message}");
                            return LpcValue.Create(0);
                        }
                    }
"""

if "c.Name == \"sprintf\"" not in content:
    # 尋找一個安全的錨點插入
    content = content.replace("if (c.Name == \"this_player\")", new_efuns + "\n                    if (c.Name == \"this_player\")")
    with open("LithosNet.VM/Interpreter.cs", "w", encoding="utf-8") as f:
        f.write(content)
    print("✅ Interpreter.cs 已成功注入 sprintf, tell_object, shout, native_call (FFI)！")
else:
    print("ℹ️ Efun 已存在，跳過注入。")

