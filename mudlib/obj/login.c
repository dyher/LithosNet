// Lithos.NET 的登入物件
int max_hp = 1000;

// 【Apply】當玩家連線時，Driver 會自動呼叫這個函數！
void logon() {
    debug_message("歡迎來到 Lithos.NET 的世界！");
    debug_message("這裡沒有 Telnet 狀態機，只有純粹的二進位效能！");
}

// 驗證函數
int verify_login(string user, string pass) {
    return 1;
}
