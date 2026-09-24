import re

with open('LithosNet.VM/BuiltInEfuns.cs', 'r', encoding='utf-8') as f:
    code = f.read()

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

        [Efun("abs")]
        public static LpcValue AbsEfun(LpcValue[] args) {
            if (args.Length < 1) return LpcValue.Create(0);
            return LpcValue.Create(System.Math.Abs(args[0].AsInt()));
        }

        // ==========================================
        // 🔪 【MMORPG 核心】字串處理
        // ==========================================
        [Efun("replace_string")]
        public static LpcValue ReplaceString(LpcValue[] args) {
            if (args.Length < 3) return LpcValue.Create("");
            return LpcValue.Create(args[0].AsString().Replace(args[1].AsString(), args[2].AsString()));
        }

        // ==========================================
        // ⏱️ 【MMORPG 核心】時間矩陣
        // ==========================================
        [Efun("time")]
        public static LpcValue TimeEfun(LpcValue[] args) {
            return LpcValue.Create((int)(System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        }
"""

if '[Efun("random")]' not in code:
    if '[Efun("tell_object")]' in code:
        code = code.replace('[Efun("tell_object")]', new_efuns + '\n        [Efun("tell_object")]')
    else:
        last_brace = code.rfind('}')
        code = code[:last_brace] + new_efuns + code[last_brace:]
        
    with open('LithosNet.VM/BuiltInEfuns.cs', 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ 已成功注入 random, replace_string, time, abs 且不衝突！")
else:
    print("ℹ️ Efun 已存在。")
