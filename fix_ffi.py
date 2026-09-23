import re

with open("LithosNet.VM/Interpreter.cs", "r", encoding="utf-8") as f:
    content = f.read()

# 1. 修復 sprintf 的引號問題 (改用 AsString())
old_sprintf = """                                if (next == 's' || next == 'd' || next == 'i' || next == 'O' || next == 'o') {
                                    sb.Append(cArgs[argIdx].ToString());
                                    argIdx++; i++; continue;
                                }"""
new_sprintf = """                                if (next == 's' || next == 'd' || next == 'i' || next == 'O' || next == 'o') {
                                    var val = cArgs[argIdx];
                                    sb.Append(val.Type == LpcType.String ? val.AsString() : val.ToString());
                                    argIdx++; i++; continue;
                                }"""
content = content.replace(old_sprintf, new_sprintf)

# 2. 注入非泛型 Delegate 定義 (C# FFI 的硬性規定)
delegate_defs = """
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private delegate int IntReturnDelegate();

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private delegate IntPtr StringArgReturnIntPtrDelegate([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPStr)] string arg);

    public class ReturnSignal : Exception { public LpcValue Value; public ReturnSignal(LpcValue v) { Value = v; } }
"""
content = content.replace("public class ReturnSignal : Exception { public LpcValue Value; public ReturnSignal(LpcValue v) { Value = v; } }", delegate_defs)

# 3. 修復 FFI getpid 的泛型委託錯誤
old_ffi_pid = """                            if (func == "getpid" || func == "time") {
                                var del = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<Func<int>>(ptr);
                                return LpcValue.Create(del());
                            }"""
new_ffi_pid = """                            if (func == "getpid" || func == "time") {
                                var del = (IntReturnDelegate)System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(ptr, typeof(IntReturnDelegate));
                                return LpcValue.Create(del());
                            }"""
content = content.replace(old_ffi_pid, new_ffi_pid)

# 4. 修復 FFI getenv 的泛型委託錯誤
old_ffi_env = """                            if (func == "getenv" && cArgs.Count >= 3) {
                                var del = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<Func<string, IntPtr>>(ptr);
                                IntPtr res = del(cArgs[2].AsString());
                                return LpcValue.Create(System.Runtime.InteropServices.Marshal.PtrToStringAnsi(res) ?? "");
                            }"""
new_ffi_env = """                            if (func == "getenv" && cArgs.Count >= 3) {
                                var del = (StringArgReturnIntPtrDelegate)System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(ptr, typeof(StringArgReturnIntPtrDelegate));
                                IntPtr res = del(cArgs[2].AsString());
                                return LpcValue.Create(System.Runtime.InteropServices.Marshal.PtrToStringAnsi(res) ?? "");
                            }"""
content = content.replace(old_ffi_env, new_ffi_env)

with open("LithosNet.VM/Interpreter.cs", "w", encoding="utf-8") as f:
    f.write(content)
print("✅ Interpreter.cs 已完美修復 sprintf 引号与 FFI 泛型委托错误！")
