import re

with open("LithosNet.VM/Interpreter.cs", "r", encoding="utf-8") as f:
    content = f.read()

# 將 send_to_user 嚴格綁定到 this.ObjectName (this_object)
old_send = 'string target = SessionManager.CurrentPlayer.Value ?? this.ObjectName;'
new_send = 'string target = this.ObjectName; // 【FluffOS 語意】嚴格發給當前執行的物件 (this_object)'

if old_send in content:
    content = content.replace(old_send, new_send)
    with open("LithosNet.VM/Interpreter.cs", "w", encoding="utf-8") as f:
        f.write(content)
    print("✅ Interpreter.cs: send_to_user 已嚴格綁定 this_object()！")

# 確保 this_player() Efun 存在
if 'c.Name == "this_player"' not in content:
    tp = '                    if (c.Name == "this_player") return LpcValue.Create(SessionManager.CurrentPlayer.Value ?? "");\n'
    content = content.replace('if (c.Name == "this_object")', tp + '                    if (c.Name == "this_object")')
    with open("LithosNet.VM/Interpreter.cs", "w", encoding="utf-8") as f:
        f.write(content)
    print("✅ Interpreter.cs: this_player() Efun 已確保存在！")
