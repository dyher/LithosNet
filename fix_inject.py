with open("LithosNet.VM/BuiltInEfuns.cs", "r", encoding="utf-8") as f:
    efun_content = f.read()

mmorpg_efuns = """
        // 【MMORPG Efun】註冊實體到空間網格
        [Efun("map_register")]
        public static LpcValue MapRegister(LpcValue[] args) {
            if (args.Length >= 3) GridMapManager.Register(args[0].AsString(), args[1].AsInt(), args[2].AsInt());
            return LpcValue.Create(1);
        }

        // 【MMORPG Efun】移動實體
        [Efun("map_move")]
        public static LpcValue MapMove(LpcValue[] args) {
            if (args.Length >= 3) GridMapManager.Move(args[0].AsString(), args[1].AsInt(), args[2].AsInt());
            return LpcValue.Create(1);
        }

        // 【MMORPG Efun】AOE 範圍查詢 (效能碾壓 rAthena 的核心)
        [Efun("get_objects_in_radius")]
        public static LpcValue GetObjectsInRadius(LpcValue[] args) {
            if (args.Length < 3) return LpcValue.Create(new List<LpcValue>());
            var list = GridMapManager.GetObjectsInRadius(args[0].AsInt(), args[1].AsInt(), args[2].AsInt());
            var lpcList = new List<LpcValue>();
            foreach(var s in list) lpcList.Add(LpcValue.Create(s));
            return LpcValue.Create(lpcList);
        }
"""

if "[Efun(\"map_register\")]" not in efun_content:
    # 【絕對安全】精準插入到 public static class BuiltInEfuns { 之後
    efun_content = efun_content.replace("public static class BuiltInEfuns {", "public static class BuiltInEfuns {\n" + mmorpg_efuns)
    with open("LithosNet.VM/BuiltInEfuns.cs", "w", encoding="utf-8") as f:
        f.write(efun_content)
    print("✅ BuiltInEfuns.cs 已完美注入 MMORPG 空間 Efun！")
else:
    print("ℹ️ Efun 已存在！")
