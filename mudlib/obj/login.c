void logon() {
    debug_message("玩家正在登入...");
    
    // 設定一個 3 秒後的定時器，呼叫本物件的 spawn_mob()
    call_out("spawn_mob", 3, "烈焰巨龍");
    
    debug_message("定時器已設定，3秒後怪物將甦醒！");
}

// 這個函數將在 3 秒後被底層 C# Task 自動回呼！
void spawn_mob(string mob_name) {
    debug_message("⚠️ 警告：一隻 " + mob_name + " 甦醒了！");
}

int verify_login(string user, string pass) {
    if (user == "admin") {
        if (pass == "123456") return 1;
    }
    return 0;
}
