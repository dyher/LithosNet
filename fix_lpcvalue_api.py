with open("LithosNet.Host/Program.cs", "r", encoding="utf-8") as f:
    prog = f.read()

# 1. 將所有瞎猜的 .Value?.ToString() 替換為正統的 .AsString()
prog = prog.replace("connectRet.Value?.ToString() ?? \"\"", "connectRet.AsString()")
prog = prog.replace("connectRet.Value?.ToString()", "connectRet.AsString()")

# 2. 修復 Console.WriteLine 中的 connectRet.Value
prog = prog.replace("Value={connectRet.Value}", "Value={connectRet.AsString()}")

with open("LithosNet.Host/Program.cs", "w", encoding="utf-8") as f:
    f.write(prog)
print("✅ Program.cs 已完美修復 LpcValue API 呼叫！回歸正統 .AsString()！")
