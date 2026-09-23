// Lithos.NET Phase 13: Mapping (字典) 綜合測試

void logon() {
    debug_message("正在測試 LPC 經典的 Mapping (字典) 語法...");
    
    // 建立玩家屬性 Mapping
    mapping stats = ([ "hp": 1000, "mp": 500, "str": 99 ]);
    
    // 讀取 Mapping 中的值
    int current_hp = stats["hp"];
    debug_message("玩家當前 HP：");
    debug_int(current_hp);
    
    // 動態新增/修改 Mapping 中的值
    stats["agi"] = 50;
    stats["hp"] = 950; // 玩家受傷了
    
    debug_message("受傷後的 HP：");
    debug_int(stats["hp"]);
    
    debug_message("玩家的新屬性 AGI：");
    debug_int(stats["agi"]);
}

int verify_login(string user, string pass) {
    if (user == "admin") {
        if (pass == "123456") return 1;
    }
    return 0;
}
