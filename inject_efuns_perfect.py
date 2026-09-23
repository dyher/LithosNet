import re

with open("LithosNet.VM/BuiltInEfuns.cs", "r", encoding="utf-8") as f:
    content = f.read()

# 【核心注入】確保 Delegate 和 Efun 嚴格在 class BuiltInEfuns { 內部
inject_code = """
        // 【FFI 底層委託定義】
        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        private delegate int IntReturnDelegate();

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        private delegate IntPtr StringArgReturnIntPtrDelegate([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPStr)] string arg);

        // 【Mudlib 靈魂】sprintf 格式化輸出
        [Efun("sprintf")]
        public static LpcValue Sprintf(LpcValue[] args) {
            if (args.Length < 1) return LpcValue.Create("");
            string fmt = args[0].AsString();
            int argIdx = 1;
            var sb = new System.Text.StringBuilder();
            for(int i=0; i<fmt.Length; i++) {
                if (fmt[i] == '%' && i+1 < fmt.Length && argIdx < args.Length) {
                    char next = fmt[i+1];
                    if (next == 's' || next == 'd' || next == 'i') {
                        var val = args[argIdx];
                        sb.Append(val.Type == LpcType.String ? val.AsString() : val.ToString());
                        argIdx++; i++; continue;
                    }
                }
                sb.Append(fmt[i]);
            }
            return LpcValue.Create(sb.ToString());
        }

        // 【Mudlib 通訊】tell_object 與 shout (使用 GetAwaiter 確保同步執行)
        [Efun("tell_object")]
        public static LpcValue TellObject(LpcValue[] args) {
            if (args.Length >= 2) SessionManager.SendAsync(args[0].AsString(), args[1].AsString()).GetAwaiter().GetResult();
            return LpcValue.Create(1);
        }

        [Efun("shout")]
        public static LpcValue Shout(LpcValue[] args) {
            if (args.Length >= 1) {
                foreach(var s in SessionManager.GetAllSessions()) SessionManager.SendAsync(s, args[0].AsString()).GetAwaiter().GetResult();
            }
            return LpcValue.Create(1);
        }

        // 【God Mode FFI】LuaJIT 風格的 native_call
        [Efun("native_call")]
        public static LpcValue NativeCall(LpcValue[] args) {
            if (args.Length < 2) return LpcValue.Create(0);
            string lib = args[0].AsString();
            string func = args[1].AsString();
            try {
                IntPtr handle = System.Runtime.InteropServices.NativeLibrary.Load(lib);
                IntPtr ptr = System.Runtime.InteropServices.NativeLibrary.GetExport(handle, func);
                if (func == "getpid" || func == "time") {
                    var del = (IntReturnDelegate)System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(ptr, typeof(IntReturnDelegate));
                    return LpcValue.Create(del());
                }
                if (func == "getenv" && args.Length >= 3) {
                    var del = (StringArgReturnIntPtrDelegate)System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(ptr, typeof(StringArgReturnIntPtrDelegate));
                    IntPtr res = del(args[2].AsString());
                    return LpcValue.Create(System.Runtime.InteropServices.Marshal.PtrToStringAnsi(res) ?? "");
                }
                return LpcValue.Create(0);
            } catch (Exception ex) {
                Console.WriteLine($"⚠️ FFI 錯誤: {ex.Message}");
                return LpcValue.Create(0);
            }
        }
"""

if "[Efun(\"native_call\")]" not in content:
    # 精準插入到 public static class BuiltInEfuns { 之後
    content = content.replace("public static class BuiltInEfuns {", "public static class BuiltInEfuns {\n" + inject_code)
    with open("LithosNet.VM/BuiltInEfuns.cs", "w", encoding="utf-8") as f:
        f.write(content)
    print("✅ BuiltInEfuns.cs 已完美注入標準 Efun (sprintf, shout, native_call)！")
else:
    print("ℹ️ Efun 已存在！")
