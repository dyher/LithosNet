// Lithos.NET 的登入物件 (具備邏輯判斷能力！)
int max_hp = 1000;

void logon() {
    debug_message("歡迎來到 Lithos.NET 的世界！");
}

// 【核心】真正的帳號密碼驗證邏輯
int verify_login(string user, string pass) {
    if (user == "admin") {
        if (pass == "123456") {
            return 1; // 驗證通過
        }
    }
    return 0; // 驗證失敗
}
