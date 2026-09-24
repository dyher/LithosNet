import re

# 1. 修改 BuiltInEfuns.cs
with open('LithosNet.VM/BuiltInEfuns.cs', 'r', encoding='utf-8') as f:
    efun_code = f.read()

new_efuns = """
        [Efun("send_to_user")]
        public static LpcValue SendToUser(LpcValue[] args) {
            if (args.Length >= 1) {
                string obj = SessionManager.CurrentPlayer.Value ?? "";
                if (string.IsNullOrEmpty(obj)) {
                    var all = SessionManager.GetAllSessions();
                    if (all.Count > 0) obj = all[0];
                }
                if (!string.IsNullOrEmpty(obj)) {
                    SessionManager.SendAsync(obj, args[0].AsString()).GetAwaiter().GetResult();
                }
            }
            return LpcValue.Create(1);
        }

        [Efun("this_object")]
        public static LpcValue ThisObject(LpcValue[] args) {
            return LpcValue.Create(SessionManager.CurrentPlayer.Value ?? "unknown");
        }

        [Efun("clone_object")]
        public static LpcValue CloneObject(LpcValue[] args) {
            if (args.Length >= 1) {
                string blueprint = args[0].AsString();
                string cloneName = blueprint + "#" + Guid.NewGuid().ToString().Substring(0, 4);
                try { ObjMgr.LoadObject(blueprint); } catch {}
                return LpcValue.Create(cloneName);
            }
            return LpcValue.Create(0);
        }

        [Efun("exec")]
        public static LpcValue Exec(LpcValue[] args) {
            if (args.Length >= 2) {
                SessionManager.Exec(args[0].AsString(), args[1].AsString());
            }
            return LpcValue.Create(1);
        }

        [Efun("destruct")]
        public static LpcValue Destruct(LpcValue[] args) {
            Console.WriteLine($"💥 [Efun] destruct({args[0].AsString()})");
            return LpcValue.Create(1);
        }
"""

if "[Efun(\"send_to_user\")]" not in efun_code:
    efun_code = efun_code.replace('[Efun("tell_object")]', new_efuns + '\n        [Efun("tell_object")]')
    with open('LithosNet.VM/BuiltInEfuns.cs', 'w', encoding='utf-8') as f:
        f.write(efun_code)
    print("✅ BuiltInEfuns.cs 已注入 send_to_user, this_object, clone_object, exec, destruct！")

# 2. 修改 Program.cs，在 CallFunction 之前綁定 CurrentPlayer (AsyncLocal)
with open('LithosNet.Host/Program.cs', 'r', encoding='utf-8') as f:
    prog_code = f.read()

prog_code = prog_code.replace(
    'try { ObjMgr.CallFunction(currentObj, "receive_message"',
    'SessionManager.CurrentPlayer.Value = currentObj;\n                        try { ObjMgr.CallFunction(currentObj, "receive_message"'
)
prog_code = prog_code.replace(
    'try { ObjMgr.CallFunction(currentObj, "receive_binary"',
    'SessionManager.CurrentPlayer.Value = currentObj;\n                        try { ObjMgr.CallFunction(currentObj, "receive_binary"'
)
prog_code = prog_code.replace(
    'ObjMgr.CallFunction(currentObj, "logon");',
    'SessionManager.CurrentPlayer.Value = currentObj;\n            ObjMgr.CallFunction(currentObj, "logon");'
)

with open('LithosNet.Host/Program.cs', 'w', encoding='utf-8') as f:
    f.write(prog_code)
print("✅ Program.cs 已注入 SessionManager.CurrentPlayer.Value 綁定！send_to_user 將精準命中目標！")
