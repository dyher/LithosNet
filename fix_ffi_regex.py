import re

with open("LithosNet.VM/Interpreter.cs", "r", encoding="utf-8") as f:
    content = f.read()

# 1. 確保 Delegate 定義存在 (放在 class Interpreter 內部最前面)
delegate_defs = """
        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        private delegate int IntReturnDelegate();

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        private delegate IntPtr StringArgReturnIntPtrDelegate([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPStr)] string arg);
"""
if "IntReturnDelegate" not in content:
    # 精準插入到 public class Interpreter { 之後
    content = re.sub(r'(public class Interpreter \{)', r'\1\n' + delegate_defs, content, count=1)
    print("✅ 注入非泛型 Delegate 定義")

# 2. 使用 Regex 強制替換 sprintf 邏輯 (解決引號問題)
# 匹配從 if (c.Name == "sprintf" 開始，到對應的 return LpcValue.Create(sb.ToString()); }
sprintf_pattern = r'if \(c\.Name == "sprintf".*?return LpcValue\.Create\(sb\.ToString\(\)\);\s*\}'
new_sprintf = """if (c.Name == "sprintf" && cArgs.Count >= 1) {
                        string fmt = cArgs[0].AsString();
                        int argIdx = 1;
                        var sb = new System.Text.StringBuilder();
                        for(int i=0; i<fmt.Length; i++) {
                            if (fmt[i] == '%' && i+1 < fmt.Length && argIdx < cArgs.Count) {
                                char next = fmt[i+1];
                                if (next == 's' || next == 'd' || next == 'i' || next == 'O' || next == 'o') {
                                    var val = cArgs[argIdx];
                                    sb.Append(val.Type == LpcType.String ? val.AsString() : val.ToString());
                                    argIdx++; i++; continue;
                                }
                            }
                            sb.Append(fmt[i]);
                        }
                        return LpcValue.Create(sb.ToString());
                    }"""
content, count1 = re.subn(sprintf_pattern, new_sprintf, content, flags=re.DOTALL)
print(f"✅ sprintf 替換結果: {count1} 處 (解決引號問題)")

# 3. 使用 Regex 強制替換 native_call 邏輯 (解決 FFI 泛型錯誤)
# 匹配從 if (c.Name == "native_call" 開始，一直到 catch 塊結束的兩個大括號
ffi_pattern = r'if \(c\.Name == "native_call".*?return LpcValue\.Create\(0\);\s*\}\s*\}'
new_ffi = """if (c.Name == "native_call" && cArgs.Count >= 2) {
                        string lib = cArgs[0].AsString();
                        string func = cArgs[1].AsString();
                        try {
                            IntPtr handle = System.Runtime.InteropServices.NativeLibrary.Load(lib);
                            IntPtr ptr = System.Runtime.InteropServices.NativeLibrary.GetExport(handle, func);
                            
                            if (func == "getpid" || func == "time") {
                                var del = (IntReturnDelegate)System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(ptr, typeof(IntReturnDelegate));
                                return LpcValue.Create(del());
                            }
                            if (func == "getenv" && cArgs.Count >= 3) {
                                var del = (StringArgReturnIntPtrDelegate)System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(ptr, typeof(StringArgReturnIntPtrDelegate));
                                IntPtr res = del(cArgs[2].AsString());
                                return LpcValue.Create(System.Runtime.InteropServices.Marshal.PtrToStringAnsi(res) ?? "");
                            }
                            return LpcValue.Create(0);
                        } catch (Exception ex) {
                            Console.WriteLine($"⚠️ FFI 錯誤: {ex.Message}");
                            return LpcValue.Create(0);
                        }
                    }"""
content, count2 = re.subn(ffi_pattern, new_ffi, content, flags=re.DOTALL)
print(f"✅ native_call (FFI) 替換結果: {count2} 處 (解除泛型委託限制)")

if count1 == 0 or count2 == 0:
    print("⚠️ 警告：Regex 沒有匹配到目標區塊，請檢查 Interpreter.cs！")

with open("LithosNet.VM/Interpreter.cs", "w", encoding="utf-8") as f:
    f.write(content)
print("🎉 Regex 強制替換完成！")
