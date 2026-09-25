import re

with open('LithosNet.VM/BuiltInEfuns.cs', 'r', encoding='utf-8') as f:
    code = f.read()

new_efuns = """
        // ==========================================
        // 🎒 【MMORPG 核心】Mapping (字典) 操作矩陣
        // ==========================================
        [Efun("keys")]
        public static LpcValue Keys(LpcValue[] args) {
            if (args.Length < 1 || args[0].Type != LpcType.Mapping) return LpcValue.Create(new System.Collections.Generic.List<LpcValue>());
            var dict = args[0].AsMapping();
            var list = new System.Collections.Generic.List<LpcValue>();
            foreach(var k in dict.Keys) list.Add(LpcValue.Create(k));
            return LpcValue.Create(list);
        }

        [Efun("values")]
        public static LpcValue Values(LpcValue[] args) {
            if (args.Length < 1 || args[0].Type != LpcType.Mapping) return LpcValue.Create(new System.Collections.Generic.List<LpcValue>());
            var dict = args[0].AsMapping();
            var list = new System.Collections.Generic.List<LpcValue>();
            foreach(var v in dict.Values) list.Add(v);
            return LpcValue.Create(list);
        }

        [Efun("m_delete")]
        public static LpcValue MDelete(LpcValue[] args) {
            if (args.Length < 2 || args[0].Type != LpcType.Mapping) return args[0];
            var dict = args[0].AsMapping();
            dict.Remove(args[1].AsString());
            return args[0];
        }

        [Efun("element_of")]
        public static LpcValue ElementOf(LpcValue[] args) {
            if (args.Length < 1) return LpcValue.Create(0);
            if (args[0].Type == LpcType.Array) {
                var arr = args[0].AsArray();
                return arr.Count > 0 ? arr[_rng.Next(arr.Count)] : LpcValue.Create(0);
            }
            if (args[0].Type == LpcType.Mapping) {
                var dict = args[0].AsMapping();
                if (dict.Count == 0) return LpcValue.Create(0);
                var keys = new System.Collections.Generic.List<string>(dict.Keys);
                return dict[keys[_rng.Next(keys.Count)]];
            }
            return LpcValue.Create(0);
        }
"""

if '[Efun("keys")]' not in code:
    code = code.replace('[Efun("tell_object")]', new_efuns + '\n        [Efun("tell_object")]')
    with open('LithosNet.VM/BuiltInEfuns.cs', 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ Mapping 操作矩陣 (keys, values, m_delete, element_of) 已完美注入！")
else:
    print("ℹ Efun 已存在。")
