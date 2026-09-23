// Lithos.NET Phase 14: 測試跨物件通訊 (Call Other)

void logon() {
    debug_message("玩家正在登入...");
    
    // 【歷史性對接】透過 -> 運算符，跨越記憶體邊界呼叫 room.c 的函數！
    string location = room->query_name();
    int mobs = room->get_monster_count();
    
    debug_message("玩家目前位於: " + location);
    debug_message("當前房間怪物數量: ");
    debug_int(mobs);
}

int verify_login(string user, string pass) {
    if (user == "admin") {
        if (pass == "123456") return 1;
    }
    return 0;
}
