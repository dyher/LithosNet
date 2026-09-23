// login.c 負責驗證帳號，並克隆出專屬的 player 物件

// 【關鍵】logon() 現在返回一個字串，告訴 Driver 後續請把封包交給誰
string logon() {
    debug_message("登入成功！正在為您創建專屬角色...");
    
    // 克隆一個 player 實體
    string my_player = clone_object("player");
    
    // 告訴 Driver：後續的連線互動，請交給 my_player (例如 "player#1")
    return my_player; 
}

int verify_login(string user, string pass) {
    if (user == "admin") {
        if (pass == "123456") return 1;
    }
    return 0;
}
