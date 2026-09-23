// Lithos.NET 的登入物件 (具備 Array 處理能力！)

void logon() {
    debug_message("正在測試 Array 與 while 迴圈的結合...");
    
    // 建立一個包含 5 個元素的陣列
    int[] drops = [10, 25, 50, 100, 5];
    
    int total_loot = 0;
    int i = 0;
    
    // 遍歷陣列並加總 (模擬計算玩家打怪獲得的總戰利品)
    while (i < 5) {
        total_loot = total_loot + drops[i];
        i = i + 1;
    }
    
    debug_message("玩家獲得的總戰利品價值是：");
    debug_int(total_loot); // 預期輸出 190
}

int verify_login(string user, string pass) {
    if (user == "admin") {
        if (pass == "123456") return 1; 
    }
    return 0; 
}
