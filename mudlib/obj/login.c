// Lithos.NET Phase 12 綜合測試

void logon() {
    // 測試 1：字串拼接
    string greeting = "歡迎" + "來到" + " Lithos.NET！";
    debug_message(greeting);
    
    // 測試 2：for 迴圈 + sizeof()
    int[] drops = [10, 25, 50, 100, 5];
    int total = 0;
    
    for (int i = 0; i < sizeof(drops); i = i + 1) {
        total = total + drops[i];
    }
    
    debug_message("戰利品總價值：");
    debug_int(total);
    
    // 測試 3：乘法與除法
    int atk = 150;
    int def = 50;
    int dmg = (atk - def) * 3 / 2;
    debug_message("傷害計算結果：");
    debug_int(dmg);
}

int verify_login(string user, string pass) {
    if (user == "admin") {
        if (pass == "123456") return 1;
    }
    return 0;
}
