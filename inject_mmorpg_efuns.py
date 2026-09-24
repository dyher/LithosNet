import re

with open('LithosNet.VM/BuiltInEfuns.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# 確保頂部有 System.Linq 與 System.Collections.Generic
if 'using System.Linq;' not in code:
    code = 'using System.Linq;\n' + code

new_efuns = """
        // ==========================================
        // 🎲 【MMORPG 核心】隨機與數學矩陣
        // ==========================================
        private static readonly System.Random _rng = new System.Random();
        
        [Efun("random")]
        public static LpcValue RandomEfun(LpcValue[] args) {
            if (args.Length < 1) return LpcValue.Create(0);
            int max = args[0].AsInt();
            return LpcValue.Create(max > 0 ? _rng.Next(max) : 0);
        }

        // ==========================================
        // 🔪 【MMORPG 核心】字串解析與處理矩陣
        // ==========================================
        [Efun("replace_string")]
        public static LpcValue ReplaceString(LpcValue[] args) {
            if (args.Length < 3) return LpcValue.Create("");
            return LpcValue.Create(args[0].AsString().Replace(args[1].AsString(), args[2].AsString()));
        }

        [Efun("explode")]
        public static LpcValue Explode(LpcValue[] args) {
            if (args.Length < 2) return LpcValue.Create(new System.Collections.Generic.List<LpcValue>());
            var parts = args[0].AsString().Split(args[1].AsString());
            var list = new System.Collections.Generic.List<LpcValue>();
            foreach(var p in parts) list.Add(LpcValue.Create(p));
            return LpcValue.Create(list);
        }

        [Efun("implode")]
        public static LpcValue Implode(LpcValue[] args) {
            if (args.Length < 2 || args[0].Type != LpcType.Array) return LpcValue.Create("");
            var list = args[0].AsArray();
            var strs = list.Select(x => x.AsString());
            return LpcValue.Create(string.Join(args[1].AsString(), strs));
        }

        // ==========================================
        // ⏱️ 【MMORPG 核心】時間矩陣
        // ==========================================
        [Efun("time")]
        public static LpcValue TimeEfun(LpcValue[] args) {
            return LpcValue.Create((int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        }
"""

# 尋找第一個 [Efun 標籤並在其前方注入
if '[Efun("random")]' not in code:
    code = code.replace('[Efun("tell_object")]', new_efuns + '\n        [Efun("tell_object")]')
    with open('LithosNet.VM/BuiltInEfuns.cs', 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ MMORPG 基礎 Efun 矩陣 (random, explode, implode, replace_string, time) 已完美注入！")
else:
    print("ℹ️ Efun 已存在。")
