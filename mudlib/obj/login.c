void logon() {
    debug_message("玩家正在登入...");
    
    // 1. 呼叫 dragon 的 setup() 進行初始化
    dragon->setup();
    
    // 2. 呼叫 dragon 繼承自 monster 的 take_damage()
    dragon->take_damage(150);
}

int verify_login(string user, string pass) {
    if (user == "admin") {
        if (pass == "123456") return 1;
    }
    return 0;
}
