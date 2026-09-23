// Lithos.NET 的登入物件 (具備 FFI 跨語言調用能力！)
int max_hp = 1000;

void logon() {
    debug_message("歡迎來到 Lithos.NET 的世界！");
    
    // 【歷史性對接】LPC 呼叫 C# Efun，C# Efun 呼叫 C 動態庫！
    debug_message("正在透過 FFI 呼叫 C 語言底層計算戰鬥傷害...");
    
    int atk = 150;
    int def = 50;
    int final_dmg = calculate_damage(atk, def);
    
    debug_message("計算完成！最終傷害是：");
    debug_int(final_dmg);
}

int verify_login(string user, string pass) {
    if (user == "admin") {
        if (pass == "123456") {
            return 1; 
        }
    }
    return 0; 
}
