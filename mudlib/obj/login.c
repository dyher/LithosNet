// Lithos.NET 的登入物件 (具備圖靈完備能力！)

void logon() {
    debug_message("歡迎來到 Lithos.NET！正在測試 while 迴圈...");
    
    // 使用 while 迴圈計算 1 + 2 + ... + 100
    int sum = 0;
    int i = 1;
    while (i <= 100) {
        sum = sum + i;
        i = i + 1;
    }
    
    debug_message("1 到 100 的總和是：");
    debug_int(sum); // 預期輸出 5050
}

int verify_login(string user, string pass) {
    if (user == "admin") {
        if (pass == "123456") {
            return 1; 
        }
    }
    return 0; 
}
