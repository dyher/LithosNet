// Lithos.NET 的 Master Object (主控物件)
// 它是 Driver 啟動後載入的第一個物件，掌控世界的規則

void create() {
    debug_message("🌍 [Master] 世界初始化完成，Master Object 已就緒。");
}

// 【經典 Apply】當有新的 TCP 連線接入時，Driver 會自動呼叫此函數
// 它必須回傳一個字串，告訴 Driver 接下來要把連線交給哪個物件處理
string connect() {
    debug_message("🔌 [Master] 偵測到新連線，正在分配 Login 物件...");
    
    // 在這裡你可以做 IP 黑名單檢查、連線數限制等
    // 我們直接回傳 "login"，讓 Driver 去呼叫 login->logon()
    return "login";
}
